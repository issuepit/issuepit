using System.Text;
using System.Text.Json;
using IssuePit.CiCdClient.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Renci.SshNet;

namespace IssuePit.CiCdClient.Runtimes;

/// <summary>
/// CI/CD runtime that provisions an ephemeral Hetzner Cloud server, runs <c>act</c> on it
/// via SSH, then marks the server for deletion once all jobs have finished.
///
/// Configuration keys (env var style → JSON path):
/// <list type="bullet">
///   <item><c>Hetzner__ApiToken</c> — fallback API token (per-org key in the DB takes precedence)</item>
///   <item><c>Hetzner__DefaultServerType</c> — default server type, e.g. <c>cx22</c> (default: <c>cx22</c>)</item>
///   <item><c>Hetzner__DefaultLocation</c> — default Hetzner datacenter, e.g. <c>nbg1</c> (default: <c>nbg1</c>)</item>
///   <item><c>Hetzner__SshKeyName</c> — name used when importing/re-using an SSH key (default: <c>issuepit-cicd</c>)</item>
///   <item><c>Hetzner__SpinDownCooldownMinutes</c> — idle minutes before deletion (default: <c>10</c>)</item>
///   <item><c>Hetzner__WatchdogMaxHours</c> — maximum lifetime before self-destruct (default: <c>24</c>)</item>
/// </list>
/// </summary>
public class HetznerCiCdRuntime(
    ILogger<HetznerCiCdRuntime> logger,
    IConfiguration configuration,
    IServiceProvider services,
    HetznerCloudService hetznerCloud) : ICiCdRuntime
{
    // How long to wait for cloud-init to finish before timing out SSH connection attempts.
    private const int MaxBootWaitSeconds = 300;
    private const int SshPollIntervalSeconds = 10;

    // SSH username on Ubuntu 24.04 cloud images.
    private const string SshUser = "root";

    // Default server/infra settings.
    private const string DefaultServerType = "cx22";
    private const string DefaultLocation = "nbg1";
    private const string DefaultSshKeyName = "issuepit-cicd";
    private const int DefaultSpinDownCooldownMinutes = 10;
    private const int DefaultWatchdogMaxHours = 24;

    // ── Entry point ───────────────────────────────────────────────────────────────

    public async Task RunAsync(
        CiCdRun run,
        TriggerPayload trigger,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        // Resolve the Hetzner API token (project-scoped → org-scoped → appsettings).
        var apiToken = await ResolveApiTokenAsync(db, run.ProjectId, cancellationToken);
        if (string.IsNullOrWhiteSpace(apiToken))
            throw new InvalidOperationException(
                "No Hetzner API token configured. " +
                "Add an API key with provider=Hetzner for the org/project, " +
                "or set the Hetzner__ApiToken configuration key.");

        var serverType = configuration["Hetzner:DefaultServerType"] ?? DefaultServerType;
        var location = configuration["Hetzner:DefaultLocation"] ?? DefaultLocation;

        // Determine SSH key to use (auto-create if not yet stored).
        var (privateKeyPem, sshKeyId) = await EnsureSshKeyAsync(apiToken, db, cancellationToken);

        // Unique server name for this run.
        var serverName = $"issuepit-cicd-{run.Id.ToString("N")[..12]}";

        await onLogLine($"[HETZNER] Provisioning server '{serverName}' (type={serverType}, location={location})", LogStream.Stdout);
        var setupStart = DateTime.UtcNow;

        var cloudInit = BuildCloudInitScript(apiToken, configuration);

        var (serverDto, _) = await hetznerCloud.CreateServerAsync(
            apiToken, serverName, serverType, location, cloudInit, sshKeyId, cancellationToken);

        // Persist to DB so the admin dashboard can track this server.
        var dbServer = new HetznerServer
        {
            Id = Guid.NewGuid(),
            HetznerServerId = serverDto.Id,
            Name = serverName,
            Ipv4Address = serverDto.Ipv4Address,
            Ipv6Address = serverDto.Ipv6Address,
            ServerType = serverType,
            Location = location,
            Status = HetznerServerStatus.Initializing,
            OrgId = await ResolveOrgIdAsync(db, run.ProjectId, cancellationToken),
        };
        db.HetznerServers.Add(dbServer);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            dbServer.ActiveRunCount++;
            dbServer.TotalRunCount++;
            dbServer.Status = HetznerServerStatus.Running;
            await db.SaveChangesAsync(cancellationToken);

            // The IPv6 address returned by Hetzner is a CIDR block like "2a01:4f8:1:2::1/64".
            // Extract just the host part for SSH.
            var sshHost = ExtractSshHost(serverDto.Ipv6Address, serverDto.Ipv4Address);

            await onLogLine($"[HETZNER] Server created (id={serverDto.Id}, host={sshHost}). Waiting for SSH...", LogStream.Stdout);

            // Wait for SSH to become available (cloud-init is running in the background).
            await WaitForSshAsync(sshHost, privateKeyPem, onLogLine, cancellationToken);

            var setupSeconds = (int)(DateTime.UtcNow - setupStart).TotalSeconds;
            dbServer.SetupTimeSeconds = setupSeconds;
            dbServer.Status = HetznerServerStatus.Running;
            await db.SaveChangesAsync(cancellationToken);

            await onLogLine($"[HETZNER] SSH ready after {setupSeconds}s. Running CI/CD workflow...", LogStream.Stdout);

            // Clone the repository and run act.
            await RunActOverSshAsync(run, trigger, sshHost, privateKeyPem, onLogLine, cancellationToken);
        }
        finally
        {
            // Mark server as idle; the reconciler will delete it after the cooldown.
            dbServer.ActiveRunCount = Math.Max(0, dbServer.ActiveRunCount - 1);
            if (dbServer.ActiveRunCount == 0)
            {
                dbServer.Status = HetznerServerStatus.Draining;
                dbServer.LastIdleAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    // ── Cloud-init script ─────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the cloud-init user-data script that bootstraps Docker, installs <c>act</c>,
    /// and sets up a self-destruct watchdog.
    /// </summary>
    private string BuildCloudInitScript(string apiToken, IConfiguration config)
    {
        var maxHours = int.TryParse(config["Hetzner:WatchdogMaxHours"], out var h) ? h : DefaultWatchdogMaxHours;
        var actVersion = config["Hetzner:ActVersion"] ?? "latest";

        // NOTE: IPv6-only servers cannot reach apt.ubuntu.com directly on older images.
        // Ubuntu 24.04 cloud images support IPv6 apt mirrors out of the box.
        return $@"#cloud-config
package_update: true
packages:
  - curl
  - git
  - jq

runcmd:
  # --- Install Docker ---
  - |
    curl -fsSL https://get.docker.com | sh
    systemctl enable docker
    systemctl start docker

  # --- Install act ---
  - |
    if [ ""{actVersion}"" = ""latest"" ]; then
      ACT_VER=$(curl -sfL https://api.github.com/repos/nektos/act/releases/latest | jq -r .tag_name)
    else
      ACT_VER=""{actVersion}""
    fi
    ARCH=$(uname -m)
    if [ ""$ARCH"" = ""x86_64"" ]; then ARCH=""x86_64""; elif [ ""$ARCH"" = ""aarch64"" ]; then ARCH=""arm64""; fi
    curl -fsSL ""https://github.com/nektos/act/releases/download/${{ACT_VER}}/act_Linux_${{ARCH}}.tar.gz"" | tar -C /usr/local/bin -xz act
    chmod +x /usr/local/bin/act

  # --- Watchdog: delete this server after {maxHours}h if not reset ---
  - |
    cat > /usr/local/bin/hetzner-self-destruct.sh << 'WATCHDOG_EOF'
    #!/bin/bash
    # Sends a DELETE request to the Hetzner Cloud API to remove this server.
    # The server ID is resolved at runtime via the metadata service.
    SERVER_ID=$(curl -sf http://169.254.169.254/hetzner/v1/metadata/instance-id || true)
    if [ -z ""$SERVER_ID"" ]; then
      # Fall back to looking up by name
      MY_NAME=$(hostname)
      SERVER_ID=$(curl -sf -H ""Authorization: Bearer {apiToken}"" \
        ""https://api.hetzner.cloud/v1/servers?name=$MY_NAME"" \
        | jq -r '.servers[0].id // empty' || true)
    fi
    if [ -n ""$SERVER_ID"" ]; then
      curl -sf -X DELETE -H ""Authorization: Bearer {apiToken}"" \
        ""https://api.hetzner.cloud/v1/servers/$SERVER_ID"" || true
    fi
    WATCHDOG_EOF
    chmod +x /usr/local/bin/hetzner-self-destruct.sh

  # --- Schedule hard self-destruct after {maxHours}h ---
  - |
    echo ""shutdown -P +{maxHours * 60} 'Hetzner watchdog: max lifetime reached'"" | at now

  # --- Watchdog reset file (touched by CI/CD jobs to defer self-destruct) ---
  - touch /tmp/cicd-watchdog-heartbeat

  # --- Watchdog daemon: resets 1h timer while heartbeat is fresh ---
  - |
    cat > /etc/systemd/system/cicd-watchdog.service << 'UNIT_EOF'
    [Unit]
    Description=IssuePit CI/CD server watchdog
    After=network.target

    [Service]
    Type=simple
    ExecStart=/usr/local/bin/cicd-watchdog-loop.sh
    Restart=always

    [Install]
    WantedBy=multi-user.target
    UNIT_EOF

    cat > /usr/local/bin/cicd-watchdog-loop.sh << 'LOOP_EOF'
    #!/bin/bash
    # Reset the 1-hour auto-shutdown as long as /tmp/cicd-watchdog-heartbeat was touched recently.
    # If no heartbeat for 60 minutes, power off.
    while true; do
      sleep 60
      if [ -f /tmp/cicd-watchdog-heartbeat ]; then
        AGE=$(( $(date +%s) - $(stat -c %Y /tmp/cicd-watchdog-heartbeat) ))
        if [ ""$AGE"" -gt 3600 ]; then
          echo ""Watchdog: no heartbeat for ${{AGE}}s, shutting down""
          /usr/local/bin/hetzner-self-destruct.sh
          shutdown -P now 'Watchdog: idle timeout'
        fi
      fi
    done
    LOOP_EOF
    chmod +x /usr/local/bin/cicd-watchdog-loop.sh
    systemctl enable cicd-watchdog
    systemctl start cicd-watchdog

final_message: ""IssuePit CI/CD bootstrap complete (act installed, Docker running, watchdog active)""
";
    }

    // ── SSH helpers ───────────────────────────────────────────────────────────────

    private async Task WaitForSshAsync(
        string host,
        string privateKeyPem,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(MaxBootWaitSeconds);
        var attempt = 0;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempt++;

            try
            {
                using var client = BuildSshClient(host, privateKeyPem);
                await client.ConnectAsync(cancellationToken);
                client.Disconnect();
                await onLogLine($"[HETZNER] SSH available (attempt {attempt})", LogStream.Stdout);
                return;
            }
            catch (Exception ex) when (DateTime.UtcNow < deadline)
            {
                if (attempt % 3 == 0)
                    await onLogLine($"[HETZNER] Waiting for SSH ({attempt} attempts, {ex.GetType().Name})…", LogStream.Stdout);

                await Task.Delay(TimeSpan.FromSeconds(SshPollIntervalSeconds), cancellationToken);
            }
        }

        throw new TimeoutException($"Server SSH did not become available within {MaxBootWaitSeconds}s.");
    }

    private async Task RunActOverSshAsync(
        CiCdRun run,
        TriggerPayload trigger,
        string host,
        string privateKeyPem,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        using var client = BuildSshClient(host, privateKeyPem);
        await client.ConnectAsync(cancellationToken);

        try
        {
            // Wait for Docker daemon to be ready (cloud-init may still be running).
            await WaitForDockerAsync(client, onLogLine, cancellationToken);

            // Clone the repository to /workspace.
            await CloneRepositoryAsync(client, trigger, onLogLine, cancellationToken);

            // Run act.
            await RunActAsync(client, run, trigger, onLogLine, cancellationToken);
        }
        finally
        {
            client.Disconnect();
        }
    }

    private static async Task WaitForDockerAsync(
        SshClient client,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < 30; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var cmd = client.CreateCommand("docker info >/dev/null 2>&1 && echo ready");
            await cmd.ExecuteAsync(cancellationToken);
            if (cmd.Result.Trim() == "ready")
                return;
            if (i % 5 == 0)
                await onLogLine("[HETZNER] Waiting for Docker daemon…", LogStream.Stdout);
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
        throw new TimeoutException("Docker daemon did not become available within 150s.");
    }

    private static async Task CloneRepositoryAsync(
        SshClient client,
        TriggerPayload trigger,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var repoUrl = trigger.GitRepoUrl;
        if (string.IsNullOrWhiteSpace(repoUrl))
        {
            await onLogLine("[HETZNER] No GitRepoUrl specified — skipping clone.", LogStream.Stdout);
            return;
        }

        // Embed credentials into the URL if provided.
        if (!string.IsNullOrWhiteSpace(trigger.GitAuthUsername) && !string.IsNullOrWhiteSpace(trigger.GitAuthToken))
        {
            var uri = new UriBuilder(repoUrl)
            {
                UserName = Uri.EscapeDataString(trigger.GitAuthUsername),
                Password = Uri.EscapeDataString(trigger.GitAuthToken),
            };
            repoUrl = uri.ToString();
        }

        var branch = string.IsNullOrWhiteSpace(trigger.Branch) ? string.Empty : $"--branch {EscapeShell(trigger.Branch)}";
        var cloneCmd = $"git clone --depth 1 {branch} {EscapeShell(repoUrl)} /workspace";

        await onLogLine("[HETZNER] Cloning repository…", LogStream.Stdout);
        await RunSshCommandAsync(client, cloneCmd, onLogLine, cancellationToken);

        // Checkout specific commit if requested.
        if (!string.IsNullOrWhiteSpace(trigger.CommitSha))
        {
            await RunSshCommandAsync(client,
                $"git -C /workspace fetch --depth 1 origin {EscapeShell(trigger.CommitSha)} && git -C /workspace checkout {EscapeShell(trigger.CommitSha)}",
                onLogLine, cancellationToken);
        }
    }

    private static async Task RunActAsync(
        SshClient client,
        CiCdRun run,
        TriggerPayload trigger,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(trigger);

        // Platform mappings to suppress act's interactive image selection prompt.
        var platformLabels = new[] { "ubuntu-latest", "ubuntu-24.04", "ubuntu-22.04", "ubuntu-20.04" };
        var actRunnerImage = trigger.ActRunnerImage ?? "catthehacker/ubuntu:act-latest";
        var platformArgs = string.Join(" ", platformLabels.Select(l => $"-P {l}={actRunnerImage}"));

        var containerSuffix = $"-{run.Id.ToString("N")[..8]}";

        // Touch heartbeat so the watchdog doesn't kill the server during the run.
        await RunSshCommandAsync(client,
            "touch /tmp/cicd-watchdog-heartbeat",
            onLogLine, cancellationToken, logOutput: false);

        var argString = string.Join(" ", args.Select(a => EscapeShell(a)));
        var actCmd = $"cd /workspace && act {platformArgs} --container-name-suffix {containerSuffix} {argString}";

        await onLogLine($"[HETZNER] Running act: {actCmd}", LogStream.Stdout);
        await RunSshCommandAsync(client, actCmd, onLogLine, cancellationToken);
    }

    private static async Task RunSshCommandAsync(
        SshClient client,
        string command,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken,
        bool logOutput = true)
    {
        using var cmd = client.CreateCommand(command);
        var executeTask = cmd.ExecuteAsync(cancellationToken);

        // Stream output line by line while the command runs.
        if (logOutput)
        {
            using var reader = new System.IO.StreamReader(cmd.OutputStream);
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
                await onLogLine(line, LogStream.Stdout);
        }

        await executeTask;

        if (cmd.ExitStatus != 0)
            throw new InvalidOperationException(
                $"Remote command failed (exit {cmd.ExitStatus}): {cmd.Error?.Trim()}");
    }

    private static SshClient BuildSshClient(string host, string privateKeyPem)
    {
        var keyBytes = Encoding.UTF8.GetBytes(privateKeyPem);
        using var keyStream = new MemoryStream(keyBytes);
        var keyFile = new PrivateKeyFile(keyStream);
        return new SshClient(host, 22, SshUser, keyFile);
    }

    // ── API token resolution ──────────────────────────────────────────────────────

    private async Task<string?> ResolveApiTokenAsync(IssuePitDbContext db, Guid projectId, CancellationToken ct)
    {
        var orgId = await db.Projects
            .Where(p => p.Id == projectId)
            .Select(p => p.OrgId)
            .FirstOrDefaultAsync(ct);

        if (orgId == Guid.Empty)
            return configuration["Hetzner:ApiToken"];

        // Project-scoped first.
        var key = await db.ApiKeys.FirstOrDefaultAsync(
            k => k.OrgId == orgId && k.ProjectId == projectId && k.Provider == ApiKeyProvider.Hetzner, ct);

        // Org-scoped fallback.
        key ??= await db.ApiKeys.FirstOrDefaultAsync(
            k => k.OrgId == orgId && k.ProjectId == null && k.Provider == ApiKeyProvider.Hetzner, ct);

        if (key is not null)
            return DecryptApiKeyValue(key.EncryptedValue);

        return configuration["Hetzner:ApiToken"];
    }

    private static async Task<Guid?> ResolveOrgIdAsync(IssuePitDbContext db, Guid projectId, CancellationToken ct) =>
        await db.Projects
            .Where(p => p.Id == projectId)
            .Select(p => (Guid?)p.OrgId)
            .FirstOrDefaultAsync(ct);

    private static string DecryptApiKeyValue(string encryptedValue) =>
        encryptedValue.StartsWith("plain:", StringComparison.Ordinal)
            ? encryptedValue["plain:".Length..]
            : encryptedValue;

    // ── SSH key management ────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures an SSH key pair exists in Hetzner Cloud.
    /// If a key with the configured name already exists it is reused; otherwise a new one is generated
    /// and imported. Returns the private key PEM and the Hetzner SSH key ID.
    /// </summary>
    private async Task<(string PrivateKeyPem, long? SshKeyId)> EnsureSshKeyAsync(
        string apiToken,
        IssuePitDbContext db,
        CancellationToken ct)
    {
        var keyName = configuration["Hetzner:SshKeyName"] ?? DefaultSshKeyName;

        // Check for a stored private key in configuration (allows bringing your own key).
        var configuredPrivateKey = configuration["Hetzner:SshPrivateKey"];
        if (!string.IsNullOrWhiteSpace(configuredPrivateKey))
        {
            // Look up by name in Hetzner to get the ID.
            var existingKeys = await hetznerCloud.ListSshKeysAsync(apiToken, ct);
            var existing = existingKeys.FirstOrDefault(k => k.Name == keyName);
            return (configuredPrivateKey, existing?.Id);
        }

        // Generate a fresh key pair and import the public key.
        logger.LogInformation("Auto-generating SSH key pair for Hetzner CI/CD (name={KeyName})", keyName);
        var (privateKeyPem, publicKeyOpenSsh) = HetznerCloudService.GenerateRsaKeyPair();

        // Remove any existing key with the same name to allow re-import.
        var keys = await hetznerCloud.ListSshKeysAsync(apiToken, ct);
        var stale = keys.FirstOrDefault(k => k.Name == keyName);
        // We cannot delete an existing key and re-import if we don't have the private key,
        // so only import if no key with this name exists yet.
        if (stale is null)
        {
            var imported = await hetznerCloud.ImportSshKeyAsync(apiToken, keyName, publicKeyOpenSsh, ct);
            logger.LogInformation("Imported SSH key '{KeyName}' (id={Id})", keyName, imported.Id);
            return (privateKeyPem, imported.Id);
        }

        // A key with the same name already exists but we generated a new private key — it won't
        // match. Fail fast with a clear error so the operator knows what to fix.
        throw new InvalidOperationException(
            $"SSH key '{keyName}' already exists in Hetzner but no private key is stored locally. " +
            $"Set Hetzner__SshPrivateKey to reuse the existing key, or delete '{keyName}' from Hetzner first.");
    }

    // ── Utilities ─────────────────────────────────────────────────────────────────

    private static string ExtractSshHost(string? ipv6Cidr, string? ipv4)
    {
        // Prefer IPv6 (cheaper on Hetzner; IPv4 disabled for cost savings).
        if (!string.IsNullOrWhiteSpace(ipv6Cidr))
        {
            // Strip CIDR notation and use the first host address.
            var host = ipv6Cidr.Split('/')[0];
            // Hetzner assigns /64 blocks; the first address of the block is the server.
            // If it ends in "::", append "1".
            if (host.EndsWith("::"))
                host = host + "1";
            return host;
        }

        if (!string.IsNullOrWhiteSpace(ipv4))
            return ipv4;

        throw new InvalidOperationException("Server has neither IPv4 nor IPv6 address.");
    }

    private static string EscapeShell(string value)
    {
        // Wrap in single quotes and escape existing single quotes.
        return "'" + value.Replace("'", "'\\''") + "'";
    }
}
