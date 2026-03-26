using System.Reflection;
using System.Text;
using IssuePit.CiCdClient.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Renci.SshNet;

namespace IssuePit.CiCdClient.Runtimes;

/// <summary>
/// Provisions a Hetzner Cloud server, runs <c>act</c> over SSH, uploads artifacts, then
/// signals the server's self-destruct watchdog when the job is done.
///
/// Configuration keys (env var → config key):
/// <list type="bullet">
///   <item><c>Hetzner__ApiToken</c> — global/dev Hetzner API token (org-level key from the DB takes precedence).</item>
///   <item><c>Hetzner__DefaultServerType</c> — server type, e.g. "cx22" (default: "cx22").</item>
///   <item><c>Hetzner__DefaultLocation</c> — datacenter, e.g. "nbg1" (default: "nbg1").</item>
///   <item><c>Hetzner__DefaultImage</c> — OS image, e.g. "ubuntu-24.04" (default: "ubuntu-24.04").</item>
///   <item><c>Hetzner__IdleTimeoutMinutes</c> — idle spin-down window in minutes (default: 15).</item>
///   <item><c>Hetzner__HardLimitHours</c> — absolute server lifetime in hours (default: 24).</item>
///   <item><c>Hetzner__SshConnectTimeoutSeconds</c> — max seconds to wait for SSH after provisioning (default: 300).</item>
///   <item><c>CiCd__ActBinaryPath</c> — path to the <c>act</c> binary on the remote server (default: "/usr/local/bin/act").</item>
///   <item><c>CiCd__ActImage</c> — runner image for act platform mapping (default: "catthehacker/ubuntu:act-latest").</item>
/// </list>
/// </summary>
public class HetznerCiCdRuntime(
    ILogger<HetznerCiCdRuntime> logger,
    IConfiguration configuration,
    HetznerCloudService hetzner,
    IServiceProvider services) : ICiCdRuntime
{
    private const string StateDir = "/run/issuepit";
    private const string WatchdogScript = "/usr/local/bin/issuepit-watchdog.sh";
    private const string SshUser = "root";

    private int SshConnectTimeoutSeconds =>
        int.TryParse(configuration["Hetzner:SshConnectTimeoutSeconds"], out var v) ? v : 300;
    private int IdleTimeoutMinutes =>
        int.TryParse(configuration["Hetzner:IdleTimeoutMinutes"], out var v) ? v : 15;
    private int HardLimitHours =>
        int.TryParse(configuration["Hetzner:HardLimitHours"], out var v) ? v : 24;

    public async Task RunAsync(
        CiCdRun run,
        TriggerPayload trigger,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        // Resolve API token: org-level DB key → global config key
        var apiToken = await ResolveApiTokenAsync(run.Project.OrgId, cancellationToken);
        if (string.IsNullOrWhiteSpace(apiToken))
            throw new InvalidOperationException(
                "Hetzner API token is not configured. Add an API key with provider 'Hetzner' to the organization, " +
                "or set the Hetzner__ApiToken configuration value.");

        // Determine server type: trigger override → config default
        var serverType = !string.IsNullOrWhiteSpace(trigger.HetznerServerType)
            ? trigger.HetznerServerType
            : configuration["Hetzner:DefaultServerType"] ?? hetzner.DefaultServerType;
        var location = configuration["Hetzner:DefaultLocation"] ?? hetzner.DefaultLocation;
        var image = configuration["Hetzner:DefaultImage"] ?? hetzner.DefaultImage;

        // Ensure an SSH key exists in the Hetzner project
        await onLogLine("[Hetzner] Ensuring SSH key is available in Hetzner project…", LogStream.Stdout);
        var (sshKeyId, privateKeyPem) = await hetzner.EnsureSshKeyAsync(apiToken, cancellationToken);

        // Persist the server record before provisioning (so admin UI shows it immediately)
        var serverRecord = new HetznerServer
        {
            Id = Guid.NewGuid(),
            OrgId = run.Project.OrgId,
            Name = $"issuepit-cicd-{run.Id:N}"[..24],
            ServerType = serverType,
            Location = location,
            Status = HetznerServerStatus.Provisioning,
            HetznerSshKeyId = sshKeyId,
            SshPrivateKey = string.IsNullOrEmpty(privateKeyPem) ? null : $"plain:{privateKeyPem}",
        };

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            db.HetznerServers.Add(serverRecord);
            await db.SaveChangesAsync(cancellationToken);
        }

        try
        {
            await ProvisionAndRunAsync(
                run, trigger, onLogLine, apiToken, sshKeyId, privateKeyPem, serverRecord,
                serverType, location, image, cancellationToken);
        }
        catch
        {
            await UpdateServerStatusAsync(serverRecord.Id, HetznerServerStatus.Failed, cancellationToken);
            throw;
        }
    }

    private async Task ProvisionAndRunAsync(
        CiCdRun run,
        TriggerPayload trigger,
        Func<string, LogStream, Task> onLogLine,
        string apiToken,
        long sshKeyId,
        string privateKeyPem,
        HetznerServer serverRecord,
        string serverType,
        string location,
        string image,
        CancellationToken cancellationToken)
    {
        var watchdogScript = LoadEmbeddedScript("hetzner-watchdog.sh");
        var cloudInit = BuildCloudInit(apiToken, watchdogScript);

        await onLogLine($"[Hetzner] Provisioning server (type={serverType}, location={location})…", LogStream.Stdout);
        var setupStart = DateTime.UtcNow;

        var created = await hetzner.CreateServerAsync(
            apiToken,
            serverRecord.Name,
            serverType,
            location,
            image,
            sshKeyId,
            cloudInit,
            cancellationToken);

        var ipv6 = created.PublicNet?.Ipv6?.Ip;
        await UpdateServerFieldsAsync(serverRecord.Id, s =>
        {
            s.HetznerServerId = created.Id;
            s.Ipv6Address = ipv6;
            s.Status = HetznerServerStatus.Initializing;
        }, cancellationToken);

        await onLogLine(
            $"[Hetzner] Server created (id={created.Id}, ipv6={ipv6 ?? "n/a"}). Waiting for SSH…",
            LogStream.Stdout);

        // Retrieve the private key — either freshly generated or from DB (if server was pre-existing)
        var effectivePrivateKey = privateKeyPem;
        if (string.IsNullOrEmpty(effectivePrivateKey))
        {
            effectivePrivateKey = await LoadPrivateKeyFromDbAsync(serverRecord.Id, cancellationToken);
        }

        if (string.IsNullOrEmpty(effectivePrivateKey))
            throw new InvalidOperationException(
                "No SSH private key available. The Hetzner SSH key for this project already existed before " +
                "IssuePit created it, so the private key cannot be recovered. Please delete the existing " +
                $"'{configuration["Hetzner:SshKeyName"] ?? "issuepit-cicd"}' key from your Hetzner project " +
                "and let IssuePit recreate it.");

        // Wait until SSH is reachable (cloud-init is still running but SSH starts early)
        var sshHost = ipv6?.TrimEnd('/').Split('/')[0];
        if (string.IsNullOrEmpty(sshHost))
            throw new InvalidOperationException(
                $"Server {created.Id} has no IPv6 address. Cannot connect via SSH.");

        var sshClient = await WaitForSshAsync(sshHost, effectivePrivateKey, onLogLine, cancellationToken);
        try
        {
            var setupDuration = (int)(DateTime.UtcNow - setupStart).TotalSeconds;
            await UpdateServerFieldsAsync(serverRecord.Id, s =>
            {
                s.Status = HetznerServerStatus.Running;
                s.ReadyAt = DateTime.UtcNow;
                s.SetupDurationSeconds = setupDuration;
                s.ActiveJobCount = 1;
                s.TotalJobCount = 1;
            }, cancellationToken);

            await onLogLine(
                $"[Hetzner] SSH connected (setup took {setupDuration}s). Waiting for Docker…",
                LogStream.Stdout);

            // Wait for Docker to become available (cloud-init installs it)
            await WaitForDockerAsync(sshClient, onLogLine, cancellationToken);

            // Notify watchdog that a job is starting
            await SignalJobStartAsync(sshClient, cancellationToken);

            // Run act over SSH
            await RunActOverSshAsync(run, trigger, sshClient, onLogLine, apiToken, cancellationToken);

            // Signal job end to watchdog
            await SignalJobEndAsync(sshClient, cancellationToken);

            await UpdateServerFieldsAsync(serverRecord.Id, s =>
            {
                s.ActiveJobCount = 0;
                s.LastJobEndedAt = DateTime.UtcNow;
                s.Status = HetznerServerStatus.Draining;
            }, cancellationToken);

            // Persist runtime history for cost dashboard
            await RecordRuntimeHistoryAsync(serverRecord, cancellationToken);
        }
        finally
        {
            sshClient.Dispose();
        }
    }

    // ─── SSH helpers ──────────────────────────────────────────────────────────

    private async Task<SshClient> WaitForSshAsync(
        string host,
        string privateKeyPem,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(SshConnectTimeoutSeconds);
        var attempt = 0;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempt++;
            try
            {
                var client = BuildSshClient(host, privateKeyPem);
                client.Connect();
                await onLogLine($"[Hetzner] SSH connected on attempt {attempt}.", LogStream.Stdout);
                return client;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (attempt % 10 == 0)
                    await onLogLine($"[Hetzner] Waiting for SSH… (attempt {attempt}, {ex.Message})", LogStream.Stdout);
                await Task.Delay(5_000, cancellationToken);
            }
        }
        throw new TimeoutException(
            $"Timed out waiting for SSH on {host} after {SshConnectTimeoutSeconds}s ({attempt} attempts).");
    }

    private static SshClient BuildSshClient(string host, string privateKeyPem)
    {
        var keyBytes = Encoding.UTF8.GetBytes(privateKeyPem);
        using var keyStream = new MemoryStream(keyBytes);
        var keyFile = new PrivateKeyFile(keyStream);

        // Hetzner servers expose SSH on the /64 IPv6 prefix; we need the host address only
        var cleanHost = host.Contains('/') ? host.Split('/')[0] : host;
        return new SshClient(cleanHost, 22, SshUser, keyFile);
    }

    private async Task WaitForDockerAsync(
        SshClient ssh,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddMinutes(10);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var cmd = ssh.CreateCommand("docker info >/dev/null 2>&1 && echo OK");
            await cmd.ExecuteAsync(cancellationToken);
            if (cmd.ExitStatus == 0 && cmd.Result.Contains("OK")) return;
            await onLogLine("[Hetzner] Waiting for Docker daemon…", LogStream.Stdout);
            await Task.Delay(5_000, cancellationToken);
        }
        throw new TimeoutException("Docker daemon did not become available within 10 minutes.");
    }

    private static async Task SignalJobStartAsync(SshClient ssh, CancellationToken ct)
    {
        using var cmd = ssh.CreateCommand(
            $"mkdir -p {StateDir} && " +
            $"echo $(( $(cat {StateDir}/active_jobs 2>/dev/null || echo 0) + 1 )) > {StateDir}/active_jobs");
        await cmd.ExecuteAsync(ct);
    }

    private static async Task SignalJobEndAsync(SshClient ssh, CancellationToken ct)
    {
        using var cmd = ssh.CreateCommand(
            $"echo 0 > {StateDir}/active_jobs && date +%s > {StateDir}/last_job_end");
        await cmd.ExecuteAsync(ct);
    }

    // ─── act execution ────────────────────────────────────────────────────────

    private async Task RunActOverSshAsync(
        CiCdRun run,
        TriggerPayload trigger,
        SshClient ssh,
        Func<string, LogStream, Task> onLogLine,
        string apiToken,
        CancellationToken cancellationToken)
    {
        // Clone the repository first
        if (!string.IsNullOrWhiteSpace(trigger.GitRepoUrl))
        {
            await onLogLine("[Hetzner] Cloning repository…", LogStream.Stdout);

            // Use GIT_ASKPASS to provide credentials without embedding them in the URL or
            // command line (which would appear in process listings and SSH command output).
            string cloneCmd;
            if (!string.IsNullOrEmpty(trigger.GitAuthUsername) && !string.IsNullOrEmpty(trigger.GitAuthToken))
            {
                // Write a minimal askpass helper to a tmpfs file and execute via GIT_ASKPASS
                var escapedUser = EscapeShell(trigger.GitAuthUsername);
                var escapedToken = EscapeShell(trigger.GitAuthToken);
                var setupCredentials =
                    $"printf '%s' {escapedUser} > /run/issuepit/.git_user && " +
                    $"printf '%s' {escapedToken} > /run/issuepit/.git_token && " +
                    $"chmod 600 /run/issuepit/.git_user /run/issuepit/.git_token && " +
                    // Create askpass helper: echoes username for "Username" prompts, token otherwise
                    "printf '#!/bin/sh\\ncase \"$1\" in *Username*) cat /run/issuepit/.git_user;; *) cat /run/issuepit/.git_token;; esac\\n' > /run/issuepit/.askpass && " +
                    "chmod 700 /run/issuepit/.askpass";
                await RunSshCommandStreamingAsync(ssh, setupCredentials, onLogLine, cancellationToken);

                var branch = trigger.Branch ?? "HEAD";
                cloneCmd =
                    $"GIT_ASKPASS=/run/issuepit/.askpass " +
                    $"GIT_TERMINAL_PROMPT=0 " +
                    $"git clone --depth 1 --branch {EscapeShell(branch)} {EscapeShell(trigger.GitRepoUrl)} /workspace 2>&1";
            }
            else
            {
                var branch = trigger.Branch ?? "HEAD";
                cloneCmd = $"git clone --depth 1 --branch {EscapeShell(branch)} {EscapeShell(trigger.GitRepoUrl)} /workspace 2>&1";
            }

            await RunSshCommandStreamingAsync(ssh, cloneCmd, onLogLine, cancellationToken);
        }

        // Build act command
        var actBin = configuration["CiCd:ActBinaryPath"] ?? "/usr/local/bin/act";
        var actRunnerImage = !string.IsNullOrWhiteSpace(trigger.ActRunnerImage)
            ? trigger.ActRunnerImage
            : configuration["CiCd:ActImage"] ?? "catthehacker/ubuntu:act-latest";

        var platformLabels = new[] { "ubuntu-latest", "ubuntu-24.04", "ubuntu-22.04", "ubuntu-20.04" };
        var actArgs = new StringBuilder();
        actArgs.Append($"-C /workspace");

        if (!string.IsNullOrWhiteSpace(trigger.Workflow))
            actArgs.Append($" -W {EscapeShell(trigger.Workflow)}");

        if (!string.IsNullOrWhiteSpace(trigger.EventName))
            actArgs.Append($" {EscapeShell(trigger.EventName)}");

        foreach (var label in platformLabels)
            actArgs.Append($" -P {EscapeShell(label)}={EscapeShell(actRunnerImage)}");

        if (!string.IsNullOrWhiteSpace(trigger.ActEnv))
        {
            foreach (var line in trigger.ActEnv.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                if (line.Contains('='))
                    actArgs.Append($" --env {EscapeShell(line.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(trigger.ActSecrets))
        {
            foreach (var line in trigger.ActSecrets.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                if (line.Contains('='))
                    actArgs.Append($" --secret {EscapeShell(line.Trim())}");
        }

        if (trigger.Inputs is { Count: > 0 })
        {
            foreach (var (key, value) in trigger.Inputs)
                actArgs.Append($" --input {EscapeShell($"{key}={value}")}");
        }

        if (!string.IsNullOrWhiteSpace(trigger.CustomArgs))
            actArgs.Append($" {trigger.CustomArgs}");

        var fullActCmd = $"HCLOUD_TOKEN={EscapeShell(apiToken)} {actBin} {actArgs} 2>&1";

        await onLogLine($"[Hetzner] Running act: {actBin} {actArgs}", LogStream.Stdout);
        await RunSshCommandStreamingAsync(ssh, fullActCmd, onLogLine, cancellationToken);
    }

    private static async Task RunSshCommandStreamingAsync(
        SshClient ssh,
        string command,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        using var cmd = ssh.CreateCommand(command);
        var asyncResult = cmd.BeginExecute();

        using var stdoutReader = new StreamReader(cmd.OutputStream);
        using var stderrReader = new StreamReader(cmd.ExtendedOutputStream);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stdoutLine = await stdoutReader.ReadLineAsync(cancellationToken);
            if (stdoutLine != null)
                await onLogLine(stdoutLine, LogStream.Stdout);

            var stderrLine = await stderrReader.ReadLineAsync(cancellationToken);
            if (stderrLine != null)
                await onLogLine(stderrLine, LogStream.Stderr);

            if (asyncResult.IsCompleted && stdoutLine is null && stderrLine is null)
                break;

            if (!asyncResult.IsCompleted)
                await Task.Delay(50, cancellationToken);
        }

        cmd.EndExecute(asyncResult);

        if (cmd.ExitStatus != 0)
            throw new InvalidOperationException(
                $"Remote command exited with code {cmd.ExitStatus}.");
    }

    // ─── Cloud-init ───────────────────────────────────────────────────────────

    private string BuildCloudInit(string apiToken, string watchdogScript)
    {
        var actVersion = configuration["Hetzner:ActVersion"] ?? "0.2.78";
        var nodeMajor = configuration["Hetzner:NodeMajorVersion"] ?? "24";
        var dotnetChannel = configuration["Hetzner:DotnetSdkChannel"] ?? "10.0";
        var s5cmdVersion = configuration["Hetzner:S5cmdVersion"] ?? "2.3.0";
        var goVersion = configuration["Hetzner:GoVersion"] ?? "1.24.2";
        var actionlintVersion = configuration["Hetzner:ActionlintVersion"] ?? "1.7.11";

        var sb = new StringBuilder();
        sb.AppendLine("#cloud-config");
        sb.AppendLine("package_update: true");
        sb.AppendLine("package_upgrade: false");
        sb.AppendLine("packages:");
        // Base packages — docker.io is NOT included here; Docker CE is installed below via
        // the official Docker apt repository (matching Dockerfile.helper-act / Dockerfile.helper-act-runner).
        sb.AppendLine("  - git");
        sb.AppendLine("  - curl");
        sb.AppendLine("  - wget");
        sb.AppendLine("  - gnupg");
        sb.AppendLine("  - ca-certificates");
        sb.AppendLine("  - apt-transport-https");
        sb.AppendLine("  - jq");
        sb.AppendLine("  - unzip");
        sb.AppendLine("  - ffmpeg");
        sb.AppendLine("runcmd:");

        // ── Docker CE (matching Dockerfile.helper-act) ──────────────────────
        sb.AppendLine("  - install -m 0755 -d /etc/apt/keyrings");
        sb.AppendLine("  - curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc");
        sb.AppendLine("  - chmod a+r /etc/apt/keyrings/docker.asc");
        sb.AppendLine("  - bash -c \". /etc/os-release && echo 'deb [arch='$(dpkg --print-architecture)' signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu '${VERSION_CODENAME}' stable' > /etc/apt/sources.list.d/docker.list\"");
        sb.AppendLine("  - apt-get update -qq");
        sb.AppendLine("  - apt-get install -y --no-install-recommends docker-ce docker-ce-cli containerd.io");
        sb.AppendLine("  - systemctl enable docker && systemctl start docker");

        // ── Node.js + npm via NodeSource (matching Dockerfile.helper-base) ──
        sb.AppendLine($"  - curl -fsSL https://deb.nodesource.com/setup_{nodeMajor}.x -o /tmp/nodesource-setup.sh");
        sb.AppendLine("  - bash /tmp/nodesource-setup.sh");
        sb.AppendLine("  - apt-get install -y --no-install-recommends nodejs");
        sb.AppendLine("  - rm -f /tmp/nodesource-setup.sh");

        // ── .NET SDK via dotnet-install.sh (matching Dockerfile.helper-base) ─
        sb.AppendLine("  - curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh");
        sb.AppendLine("  - chmod +x /tmp/dotnet-install.sh");
        sb.AppendLine($"  - /tmp/dotnet-install.sh --channel {dotnetChannel} --install-dir /usr/share/dotnet");
        sb.AppendLine("  - ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet");
        sb.AppendLine("  - rm -f /tmp/dotnet-install.sh");

        // ── Go (matching Dockerfile.helper-act-runner) ──────────────────────
        sb.AppendLine($"  - curl -fsSL https://go.dev/dl/go{goVersion}.linux-amd64.tar.gz | tar -C /usr/local -xz");
        sb.AppendLine("  - ln -sf /usr/local/go/bin/go /usr/local/bin/go");
        sb.AppendLine("  - ln -sf /usr/local/go/bin/gofmt /usr/local/bin/gofmt");

        // ── s5cmd — S3-compatible artifact upload (matching Dockerfile.helper-base) ──
        sb.AppendLine($"  - curl --proto '=https' --tlsv1.2 -fsSL https://github.com/peak/s5cmd/releases/download/v{s5cmdVersion}/s5cmd_{s5cmdVersion}_Linux-64bit.tar.gz | tar -xz -C /usr/local/bin s5cmd");

        // ── act binary from pinned GitHub release ────────────────────────────
        sb.AppendLine($"  - curl -fsSL https://github.com/nektos/act/releases/download/v{actVersion}/act_Linux_x86_64.tar.gz | tar xz -C /usr/local/bin act");
        sb.AppendLine("  - chmod +x /usr/local/bin/act");

        // ── actionlint (matching Dockerfile.helper-act) ──────────────────────
        sb.AppendLine($"  - curl --proto '=https' --tlsv1.2 -fsSL https://raw.githubusercontent.com/rhysd/actionlint/main/scripts/download-actionlint.bash -o /tmp/download-actionlint.bash");
        sb.AppendLine($"  - bash /tmp/download-actionlint.bash {actionlintVersion} /usr/local/bin");
        sb.AppendLine("  - rm -f /tmp/download-actionlint.bash");

        // ── Secure token file (tmpfs, never in process env or cloud-init log) ─
        sb.AppendLine("  - mkdir -p /run/issuepit && chmod 700 /run/issuepit");
        sb.AppendLine($"  - printf '%s' '{apiToken.Replace("'", "'\\''")}' > /run/issuepit/.hcloud_token");
        sb.AppendLine("  - chmod 600 /run/issuepit/.hcloud_token");

        // ── Watchdog ─────────────────────────────────────────────────────────
        sb.AppendLine("  - |");
        sb.AppendLine("    cat > /usr/local/bin/issuepit-watchdog.sh << 'WATCHDOG_EOF'");
        sb.Append(watchdogScript);
        sb.AppendLine("WATCHDOG_EOF");
        sb.AppendLine("  - chmod +x /usr/local/bin/issuepit-watchdog.sh");
        // Start watchdog — token is read from the secure tmpfs file, not passed in the environment
        sb.AppendLine($"  - >-");
        sb.AppendLine($"    HCLOUD_TOKEN=$(cat /run/issuepit/.hcloud_token)");
        sb.AppendLine($"    HARD_LIMIT_HOURS={HardLimitHours}");
        sb.AppendLine($"    IDLE_TIMEOUT_MINUTES={IdleTimeoutMinutes}");
        sb.AppendLine($"    nohup /usr/local/bin/issuepit-watchdog.sh </dev/null >>/var/log/issuepit-watchdog.log 2>&1 &");
        sb.AppendLine("  - echo 0 > /run/issuepit/active_jobs");
        sb.AppendLine($"  - date +%s > /run/issuepit/last_job_end");

        return sb.ToString();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string LoadEmbeddedScript(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private async Task<string?> ResolveApiTokenAsync(Guid orgId, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var key = await db.ApiKeys
            .Where(k => k.OrgId == orgId && k.Provider == ApiKeyProvider.Hetzner)
            .OrderBy(k => k.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (key != null)
        {
            var raw = key.EncryptedValue;
            return raw.StartsWith("plain:") ? raw["plain:".Length..] : raw;
        }

        return configuration["Hetzner:ApiToken"];
    }

    private async Task<string?> LoadPrivateKeyFromDbAsync(Guid serverId, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var server = await db.HetznerServers.FindAsync([serverId], ct);
        if (server?.SshPrivateKey is null) return null;
        var raw = server.SshPrivateKey;
        return raw.StartsWith("plain:") ? raw["plain:".Length..] : raw;
    }

    private async Task UpdateServerStatusAsync(Guid serverId, HetznerServerStatus status, CancellationToken ct)
    {
        await UpdateServerFieldsAsync(serverId, s => s.Status = status, ct);
    }

    private async Task UpdateServerFieldsAsync(Guid serverId, Action<HetznerServer> update, CancellationToken ct)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var server = await db.HetznerServers.FindAsync([serverId], ct);
            if (server is null) return;
            update(server);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update HetznerServer {ServerId} in DB", serverId);
        }
    }

    /// <summary>
    /// Writes a <see cref="HetznerServerRuntimeHistory"/> row so the cost dashboard
    /// can aggregate billable time and job counts across all server lifetimes.
    /// </summary>
    private async Task RecordRuntimeHistoryAsync(HetznerServer serverRecord, CancellationToken ct)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

            // Reload the latest server state (metrics may have been updated)
            var server = await db.HetznerServers.FindAsync([serverRecord.Id], ct);
            if (server is null) return;

            var now = DateTime.UtcNow;
            var totalSeconds = (int)(now - server.CreatedAt).TotalSeconds;
            var billableSeconds = server.ReadyAt.HasValue
                ? (int)(now - server.ReadyAt.Value).TotalSeconds
                : totalSeconds;

            var history = new HetznerServerRuntimeHistory
            {
                Id = Guid.NewGuid(),
                HetznerServerId = server.Id,
                OrgId = server.OrgId,
                ServerType = server.ServerType,
                Location = server.Location,
                ProvisionedAt = server.CreatedAt,
                ReadyAt = server.ReadyAt,
                DeletedAt = now,
                TotalRuntimeSeconds = totalSeconds,
                BillableSeconds = billableSeconds,
                TotalJobCount = server.TotalJobCount,
                SetupDurationSeconds = server.SetupDurationSeconds,
                PeakCpuLoadPercent = server.CpuLoadPercent,
                PeakRamUsedMb = server.RamUsedMb,
                RecordedAt = now,
            };

            db.HetznerServerRuntimeHistories.Add(history);
            await db.SaveChangesAsync(ct);
            logger.LogDebug("Recorded runtime history for server {ServerId} ({TotalSeconds}s, {Jobs} jobs)",
                server.Id, totalSeconds, server.TotalJobCount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record runtime history for server {ServerId}", serverRecord.Id);
        }
    }

    private static string EscapeShell(string value) =>
        $"'{value.Replace("'", "'\\''")}'";
}
