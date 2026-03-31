using System.Formats.Tar;
using System.Text;
using System.Text.Json;
using Docker.DotNet;
using Docker.DotNet.Models;
using IssuePit.Core.Enums;
using Microsoft.Extensions.Logging;

namespace IssuePit.DockerRuntime;

/// <summary>
/// Abstract base class shared by <c>DockerAgentRuntime</c> (ExecutionClient) and
/// <c>DockerCiCdRuntime</c> (CiCdClient).
///
/// Contains:
/// <list type="bullet">
///   <item>Core Docker exec primitives: <see cref="ExecCommandAsync"/>, <see cref="ExecReadOutputAsync"/>,
///     <see cref="ReadMultiplexedStreamAsync"/>, <see cref="StreamContainerLogsAsync"/>, <see cref="InjectFileAsync"/>.</item>
///   <item>Static URL/git helpers: <see cref="BuildAuthenticatedCloneUrl"/>, <see cref="StripOriginPrefix"/>,
///     <see cref="GenerateFeatureBranchName"/>.</item>
///   <item>Workspace setup methods that replace <c>entrypoint.sh</c> logic for the exec-flow:
///     <see cref="CloneWorkspaceAsync"/>, <see cref="SetupGitIdentityAndBranchAsync"/>,
///     <see cref="InstallGitPushWrapperAsync"/>, <see cref="StartDindAsync"/>,
///     <see cref="SetupDnsProxyAsync"/>, <see cref="SetupWorkspaceToolsAsync"/>,
///     <see cref="WriteOpencodeConfigAsync"/>.</item>
/// </list>
///
/// Moving all setup steps into C# ensures that every log line — including git clone errors,
/// DinD startup messages, and config write confirmations — flows through the
/// <c>onLogLine</c> callback and is therefore visible in the IssuePit UI.
/// Previously these steps ran inside <c>entrypoint.sh</c> before <c>docker exec</c> was
/// possible, so failures (e.g. wrong credentials, branch mismatch) silently killed the
/// container and appeared only in <c>docker logs</c>.
/// </summary>
public abstract class DockerRuntimeBase
{
    /// <summary>Read buffer size for multiplexed stream I/O. 80 KiB matches Docker SDK convention.</summary>
    private const int LogBufferSize = 81920;

    /// <summary>Domains always allowed through the DNS proxy even when DisableInternet=true.</summary>
    private static readonly IReadOnlyList<string> DefaultAllowedDomains =
    [
        "github.com",
        "npmjs.org",
        "nuget.org",
        "microsoft.com",
        "ghcr.io",
        "docker.io",
        "aspire.dev",
    ];

    /// <summary>The Docker client used by subclasses for all container operations.</summary>
    protected readonly DockerClient DockerClient;

    /// <summary>Logger used by subclasses for structured logging.</summary>
    protected readonly ILogger Logger;

    protected DockerRuntimeBase(ILogger logger, DockerClient dockerClient)
    {
        Logger = logger;
        DockerClient = dockerClient;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Core Docker exec helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes <paramref name="cmd"/> inside a running container via docker exec.
    /// Streams output to <paramref name="onLogLine"/> and returns the process exit code.
    /// </summary>
    /// <param name="env">Optional environment variables for this exec, in <c>KEY=VALUE</c> form.</param>
    /// <param name="workingDir">Working directory inside the container (default: <c>/workspace</c>).</param>
    /// <param name="logCommand">
    /// When <c>true</c>, emits a <c>[CMD] $ &lt;command&gt;</c> line via <paramref name="onLogLine"/> before
    /// executing, so the full command (including arguments) is visible in the session logs in verbose mode.
    /// Defaults to <c>false</c>. Pass <c>true</c> for any exec that does not contain sensitive values
    /// (e.g. auth tokens in URLs) and whose command line is useful for diagnostics.
    /// </param>
    protected async Task<long> ExecCommandAsync(
        string containerId,
        IReadOnlyList<string> cmd,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? env = null,
        string workingDir = "/workspace",
        bool logCommand = false)
    {
        if (logCommand)
        {
            var cmdLine = string.Join(' ', cmd.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
            // Truncate very long commands (e.g. inline shell scripts) to keep session logs readable.
            if (cmdLine.Length > 300)
                cmdLine = cmdLine[..300] + "… (truncated)";
            await onLogLine($"[CMD] $ {cmdLine}", LogStream.Stdout);
        }

        var execCreate = await DockerClient.Exec.CreateContainerExecAsync(
            containerId,
            new ContainerExecCreateParameters
            {
                Cmd = cmd.ToList(),
                AttachStdout = true,
                AttachStderr = true,
                Env = env?.ToList(),
                WorkingDir = workingDir,
            },
            cancellationToken);

        using var stream = await DockerClient.Exec.StartContainerExecAsync(
            execCreate.ID, new ContainerExecStartParameters { Detach = false }, cancellationToken);

        await ReadMultiplexedStreamAsync(stream, onLogLine, cancellationToken);

        var inspect = await DockerClient.Exec.InspectContainerExecAsync(execCreate.ID, cancellationToken);
        return inspect.ExitCode ?? 0;
    }

    /// <summary>
    /// Executes <paramref name="cmd"/> inside a running container and returns the stdout output
    /// as a trimmed string. Stderr is intentionally excluded — git and other tools emit error
    /// messages on stderr that would otherwise pollute captured values (e.g. SHA, branch name).
    /// Output is not forwarded to any log sink.
    /// </summary>
    protected async Task<string> ExecReadOutputAsync(
        string containerId,
        IReadOnlyList<string> cmd,
        CancellationToken cancellationToken,
        string workingDir = "/workspace")
    {
        var sb = new StringBuilder();
        await ExecCommandAsync(containerId, cmd,
            (line, stream) =>
            {
                if (stream == LogStream.Stdout) sb.AppendLine(line);
                return Task.CompletedTask;
            },
            cancellationToken,
            workingDir: workingDir);
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Reads a <see cref="MultiplexedStream"/> line by line and forwards each line to
    /// <paramref name="onLogLine"/>. Shared between container log streaming and docker exec output.
    /// </summary>
    protected static async Task ReadMultiplexedStreamAsync(
        MultiplexedStream stream,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[LogBufferSize];
        var remainder = string.Empty;
        var lastTarget = LogStream.Stdout;

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
            if (result.EOF) break;

            lastTarget = result.Target == MultiplexedStream.TargetStream.StandardError
                ? LogStream.Stderr
                : LogStream.Stdout;

            var text = remainder + Encoding.UTF8.GetString(buffer, 0, result.Count);
            var lines = text.Split('\n');

            for (var i = 0; i < lines.Length - 1; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (!string.IsNullOrEmpty(line))
                    await onLogLine(line, lastTarget);
            }

            remainder = lines[^1];
        }

        var flushed = remainder.TrimEnd('\r');
        if (!string.IsNullOrEmpty(flushed))
            await onLogLine(flushed, lastTarget);
    }

    /// <summary>
    /// Streams the container's stdout/stderr to <paramref name="onLogLine"/> and blocks until the
    /// stream closes. Used by the legacy (non-exec) flow only.
    /// </summary>
    protected async Task StreamContainerLogsAsync(
        string containerId,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var logsParams = new ContainerLogsParameters
        {
            Follow = true,
            ShowStdout = true,
            ShowStderr = true,
        };

        using var stream = await DockerClient.Containers.GetContainerLogsAsync(
            containerId, logsParams, cancellationToken);

        await ReadMultiplexedStreamAsync(stream, onLogLine, cancellationToken);
    }

    /// <summary>
    /// Injects a file into a running container using the Docker tar-over-HTTP archive API.
    /// Creates a tar archive containing the single file and extracts it to <paramref name="targetDirectory"/>.
    /// </summary>
    protected async Task InjectFileAsync(
        string containerId,
        string targetDirectory,
        string filename,
        byte[] content,
        UnixFileMode mode,
        CancellationToken cancellationToken)
    {
        using var tarBuffer = new MemoryStream();
        await using (var tarWriter = new TarWriter(tarBuffer, TarEntryFormat.Ustar, leaveOpen: true))
        {
            var entry = new UstarTarEntry(TarEntryType.RegularFile, filename)
            {
                Mode = mode,
                DataStream = new MemoryStream(content),
            };
            await tarWriter.WriteEntryAsync(entry, cancellationToken);
        }

        tarBuffer.Seek(0, SeekOrigin.Begin);

        await DockerClient.Containers.ExtractArchiveToContainerAsync(
            containerId,
            new CopyToContainerParameters { Path = targetDirectory },
            tarBuffer,
            cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Static URL/git helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an authenticated clone URL by injecting <paramref name="username"/> and
    /// <paramref name="token"/> into an HTTPS URL using <see cref="UriBuilder"/>.
    /// Returns the original <paramref name="url"/> unchanged when no token is provided or the
    /// URL is not HTTPS (e.g. SSH URLs do not support embedded credentials).
    /// </summary>
    protected static string BuildAuthenticatedCloneUrl(string url, string? username, string? token)
    {
        if (string.IsNullOrEmpty(token) ||
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;

        var builder = new UriBuilder(url)
        {
            UserName = Uri.EscapeDataString(string.IsNullOrEmpty(username) ? "git" : username),
            Password = Uri.EscapeDataString(token),
        };
        return builder.Uri.AbsoluteUri;
    }

    /// <summary>
    /// Strips the leading <c>origin/</c> prefix from a branch name if present.
    /// <c>git clone -b</c> expects a remote branch name (e.g. <c>main</c>), not a
    /// remote-tracking ref (e.g. <c>origin/main</c>).
    /// </summary>
    protected static string StripOriginPrefix(string branch) =>
        branch.StartsWith("origin/", StringComparison.OrdinalIgnoreCase)
            ? branch["origin/".Length..]
            : branch;

    /// <summary>
    /// Returns the IssuePit Git Server base URL from configuration, or <c>null</c> when not configured.
    /// Agents can use this to add the IssuePit git server as a remote inside containers.
    /// Configured via <c>GitServer__BaseUrl</c> (injected by AppHost into execution-client and cicd-client).
    /// </summary>
    protected static string? GetIssuePitGitServerUrl(Microsoft.Extensions.Configuration.IConfiguration config) =>
        config["GitServer__BaseUrl"];

    /// <summary>
    /// Generates a conventional-commits-style feature branch name from an issue number and title.
    /// Format: <c>{verb}/{number}-{slug}</c> (e.g. <c>fix/42-null-pointer-in-login</c>).
    /// Mirrors the branch-name logic that was previously in <c>entrypoint.sh</c>.
    /// </summary>
    protected static string GenerateFeatureBranchName(int issueNumber, string issueTitle)
    {
        var titleLower = issueTitle.ToLowerInvariant();

        var verb =
            ContainsAny(titleLower, "fix", "bug", "hotfix", "patch", "correct", "repair") ? "fix" :
            ContainsAny(titleLower, "chore", "update", "upgrade", "refactor", "clean") ? "chore" :
            ContainsAny(titleLower, "doc", "docs", "document", "readme") ? "docs" :
            ContainsAny(titleLower, "test", "spec", "coverage") ? "test" :
            "feat";

        // Slugify: lowercase, non-alphanumeric → hyphen, collapse consecutive hyphens, trim, max 30 chars.
        var slug = new StringBuilder();
        foreach (var c in titleLower)
        {
            if (char.IsLetterOrDigit(c))
                slug.Append(c);
            else if (slug.Length > 0 && slug[^1] != '-')
                slug.Append('-');
        }
        var slugStr = slug.ToString().Trim('-');
        if (slugStr.Length > 30)
            slugStr = slugStr[..30].TrimEnd('-');

        return $"{verb}/{issueNumber}-{slugStr}";
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Workspace setup — replaces entrypoint.sh steps in exec-flow containers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Clones the git repository into <c>/workspace</c> inside the container via docker exec.
    /// Sets <c>GIT_TERMINAL_PROMPT=0</c> so git fails immediately with a clear error when
    /// credentials are missing instead of waiting for interactive input.
    ///
    /// Clone strategy:
    /// <list type="number">
    ///   <item>If <paramref name="featureBranch"/> is set, try cloning it first (it may already exist).</item>
    ///   <item>If that fails (branch not in remote), fall back to <paramref name="baseBranch"/>.</item>
    ///   <item>If the base branch clone also fails, throw with a clear error message visible in the UI.</item>
    /// </list>
    ///
    /// <para>Shallow vs full-history clone:</para>
    /// <list type="bullet">
    ///   <item><description>
    ///     When <paramref name="fullHistory"/> is <c>false</c> (default) <c>--depth=1</c> is used for
    ///     a fast shallow clone. This is safe when the clone source and push target are the same remote,
    ///     because all parent objects already exist on the push target.
    ///   </description></item>
    ///   <item><description>
    ///     When <paramref name="fullHistory"/> is <c>true</c> <c>--depth=1</c> is omitted. A full-history
    ///     clone is required whenever the push target (Working remote) is a different repository from the
    ///     clone source (Release/upstream remote). Without it, <c>git push</c> would fail with
    ///     <c>remote: fatal: did not receive expected object</c> because the working remote does not have
    ///     the shallow commit's parent objects and cannot resolve the pack delta.
    ///   </description></item>
    /// </list>
    /// </summary>
    protected async Task CloneWorkspaceAsync(
        string containerId,
        string remoteUrl,
        string? baseBranch,
        string? featureBranch,
        string? authUsername,
        string? authToken,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken,
        bool fullHistory = false)
    {
        var cloneUrl = BuildAuthenticatedCloneUrl(remoteUrl, authUsername, authToken);
        // GIT_TERMINAL_PROMPT=0 prevents git from prompting for credentials, ensuring that
        // auth failures fail fast with a clear error visible in the UI instead of hanging.
        IReadOnlyList<string> gitEnv = ["GIT_TERMINAL_PROMPT=0"];

        // --depth=1 gives a fast shallow clone suitable for same-remote scenarios.
        // When fullHistory=true (different push target) we omit --depth=1 so the working
        // remote can receive a push without missing parent object errors.
        var depthArgs = fullHistory ? Array.Empty<string>() : new[] { "--depth=1" };
        if (fullHistory)
            await onLogLine("[INFO] Full-history clone (push target differs from clone source — skipping --depth=1 to ensure pushable history)", LogStream.Stdout);

        // Ensure /workspace exists before cloning.
        await ExecCommandAsync(containerId, ["mkdir", "-p", "/workspace"],
            (_, _) => Task.CompletedTask, cancellationToken, workingDir: "/");

        // Attempt feature branch first (if provided).
        if (!string.IsNullOrWhiteSpace(featureBranch))
        {
            var branch = StripOriginPrefix(featureBranch);
            await onLogLine($"[INFO] Cloning {remoteUrl} (feature branch: {branch}) into /workspace", LogStream.Stdout);
            var featureArgs = new List<string> { "git", "clone" };
            featureArgs.AddRange(depthArgs);
            featureArgs.AddRange(["-b", branch, cloneUrl, "/workspace"]);
            // Use a sink callback — feature branch absence is expected, not an error.
            var featureExitCode = await ExecCommandAsync(
                containerId, featureArgs, (_, _) => Task.CompletedTask,
                cancellationToken, env: gitEnv, workingDir: "/");

            if (featureExitCode == 0)
            {
                await onLogLine("[INFO] Feature branch cloned successfully", LogStream.Stdout);
                await StripCloneCredentialsAsync(containerId, remoteUrl, authToken, onLogLine, cancellationToken);
                return;
            }
            await onLogLine($"[INFO] Feature branch '{branch}' not found in remote; cloning base branch '{baseBranch}'", LogStream.Stdout);
        }

        // Fall back to base branch.
        if (string.IsNullOrWhiteSpace(baseBranch))
            throw new InvalidOperationException(
                $"No base branch configured for repository '{remoteUrl}'. " +
                "Set DefaultBranch on the GitRepository record.");

        var baseCloneArgs = new List<string> { "git", "clone" };
        baseCloneArgs.AddRange(depthArgs);
        baseCloneArgs.AddRange(["-b", baseBranch, cloneUrl, "/workspace"]);
        await onLogLine(!string.IsNullOrWhiteSpace(featureBranch)
            ? $"[INFO] Cloning base branch '{baseBranch}'…"
            : $"[INFO] Cloning {remoteUrl} (branch: {baseBranch}) into /workspace",
            LogStream.Stdout);

        var baseExitCode = await ExecCommandAsync(
            containerId, baseCloneArgs,
            async (line, stream) => await onLogLine(line, stream),
            cancellationToken, env: gitEnv, workingDir: "/");

        if (baseExitCode != 0)
            throw new InvalidOperationException(
                $"git clone failed (exit code {baseExitCode}) for '{remoteUrl}' (branch: '{baseBranch}'). " +
                "Verify the remote URL is accessible, the branch exists, and the auth token has read access. " +
                "To fix a branch mismatch, update GitRepository.DefaultBranch in IssuePit to match the " +
                "actual default branch of the remote.");

        await StripCloneCredentialsAsync(containerId, remoteUrl, authToken, onLogLine, cancellationToken);
    }

    /// <summary>
    /// Removes credentials embedded in the clone URL from <c>/workspace/.git/config</c> by
    /// resetting the remote URL to the plain (unauthenticated) form. This prevents auth tokens
    /// from persisting inside the agent container after the clone completes.
    /// Best-effort — a failure is logged as a warning and does not abort setup.
    /// </summary>
    private async Task StripCloneCredentialsAsync(
        string containerId,
        string remoteUrl,
        string? authToken,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(authToken))
            return; // No credentials were injected — nothing to strip.

        try
        {
            // Reset the origin URL to the unauthenticated form so the token is not stored in .git/config.
            await ExecCommandAsync(
                containerId,
                ["git", "remote", "set-url", "origin", remoteUrl],
                (_, _) => Task.CompletedTask,
                cancellationToken);
        }
        catch (Exception ex)
        {
            await onLogLine($"[WARN] Could not strip credentials from git remote URL (non-fatal): {ex.Message}", LogStream.Stderr);
        }
    }

    /// <summary>
    /// Configures the git identity in <c>/workspace</c> and checks out (or creates) the feature branch.
    /// Safe to call when no feature branch is set — in that case only the git identity is configured.
    /// </summary>
    protected async Task SetupGitIdentityAndBranchAsync(
        string containerId,
        string? featureBranch,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        // Configure git identity for commits made by the agent.
        await ExecCommandAsync(containerId,
            ["git", "config", "user.name", "IssuePit Agent"],
            (_, _) => Task.CompletedTask, cancellationToken);
        await ExecCommandAsync(containerId,
            ["git", "config", "user.email", "agent@issuepit.ai"],
            (_, _) => Task.CompletedTask, cancellationToken);

        if (!string.IsNullOrWhiteSpace(featureBranch))
        {
            var currentBranch = await ExecReadOutputAsync(
                containerId, ["git", "branch", "--show-current"], cancellationToken);

            if (currentBranch != featureBranch)
            {
                await onLogLine($"[INFO] Checking out feature branch: {featureBranch}", LogStream.Stdout);
                // Create the branch if it does not exist, or check out the existing one.
                var checkoutExit = await ExecCommandAsync(containerId,
                    ["git", "checkout", "-b", featureBranch],
                    (_, _) => Task.CompletedTask, cancellationToken);
                if (checkoutExit != 0)
                    await ExecCommandAsync(containerId,
                        ["git", "checkout", featureBranch],
                        (_, _) => Task.CompletedTask, cancellationToken);
            }
        }

        var activeBranch = await ExecReadOutputAsync(
            containerId, ["git", "branch", "--show-current"], cancellationToken);
        await onLogLine($"[INFO] Active branch: {activeBranch}", LogStream.Stdout);
    }

    /// <summary>
    /// Installs a lightweight git push-blocking wrapper at <c>/usr/local/bin/git</c> so that
    /// agent tools (opencode, codex, etc.) cannot accidentally push to the remote during their run.
    /// The execution client explicitly pushes after the agent completes.
    ///
    /// The real git binary path is saved to <c>/tmp/.issuepit-real-git</c> so that
    /// <c>EmitGitMarkersAsync</c> can bypass the wrapper when it pushes.
    /// </summary>
    protected async Task InstallGitPushWrapperAsync(
        string containerId,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        // Find the real git binary path.
        var realGit = await ExecReadOutputAsync(
            containerId,
            ["/bin/sh", "-c", "command -v git 2>/dev/null || echo /usr/bin/git"],
            cancellationToken,
            workingDir: "/");

        if (string.IsNullOrWhiteSpace(realGit))
            realGit = "/usr/bin/git";

        // Save real git path so exec callers can bypass the push-blocking wrapper.
        // Use InjectFileAsync to write the path safely without any shell interpolation.
        await ExecCommandAsync(containerId,
            ["mkdir", "-p", "/tmp"],
            (_, _) => Task.CompletedTask, cancellationToken, workingDir: "/");
        await InjectFileAsync(
            containerId, "/tmp", ".issuepit-real-git",
            Encoding.UTF8.GetBytes(realGit),
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead,
            cancellationToken);

        // Build the wrapper script that blocks `git push` and forwards everything else.
        // The real git path is embedded directly — it was obtained from `command -v git` inside
        // the container which returns a safe filesystem path (no shell metacharacters).
        var wrapperContent =
            $"#!/usr/bin/env bash\n" +
            $"# IssuePit git wrapper — blocks push; all other subcommands are forwarded unchanged.\n" +
            $"if [[ \"${{1:-}}\" == \"push\" ]]; then\n" +
            $"    echo \"[issuepit] git push is not permitted inside the agent container.\" >&2\n" +
            $"    echo \"[issuepit] The execution client will push the branch after your run completes.\" >&2\n" +
            $"    exit 1\n" +
            $"fi\n" +
            $"exec \"$(cat /tmp/.issuepit-real-git 2>/dev/null || echo /usr/bin/git)\" \"$@\"\n";

        var wrapperBytes = Encoding.UTF8.GetBytes(wrapperContent);

        await InjectFileAsync(
            containerId,
            "/usr/local/bin",
            "git",
            wrapperBytes,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
            cancellationToken);

        await onLogLine($"[INFO] git push wrapper installed (real git: {realGit})", LogStream.Stdout);
    }

    /// <summary>
    /// Restores workspace-level package dependencies after a git clone:
    /// runs <c>npm install --prefer-offline</c> when <c>package.json</c> is present, and
    /// <c>dotnet restore</c> when any <c>.csproj</c> or <c>.sln</c> file is found within
    /// 3 directory levels.
    /// Both operations are best-effort — failures are logged but never abort the session.
    /// </summary>
    protected async Task SetupWorkspaceToolsAsync(
        string containerId,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        try
        {
            // npm install
            var hasPackageJson = await ExecReadOutputAsync(
                containerId,
                ["/bin/sh", "-c", "test -f /workspace/package.json && echo yes || echo no"],
                cancellationToken);
            if (hasPackageJson.Trim() == "yes")
            {
                await onLogLine("[INFO] Running npm install", LogStream.Stdout);
                await ExecCommandAsync(containerId, ["npm", "install", "--prefer-offline"],
                    async (line, stream) => await onLogLine(line, stream), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await onLogLine($"[WARN] npm install failed (non-fatal): {ex.Message}", LogStream.Stderr);
        }

        try
        {
            // dotnet restore
            var hasCsproj = await ExecReadOutputAsync(
                containerId,
                ["/bin/sh", "-c", "find /workspace -maxdepth 3 \\( -name '*.csproj' -o -name '*.sln' \\) -quit 2>/dev/null | grep -q . && echo yes || echo no"],
                cancellationToken);
            if (hasCsproj.Trim() == "yes")
            {
                await onLogLine("[INFO] Running dotnet restore", LogStream.Stdout);
                await ExecCommandAsync(containerId, ["dotnet", "restore"],
                    async (line, stream) => await onLogLine(line, stream), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await onLogLine($"[WARN] dotnet restore failed (non-fatal): {ex.Message}", LogStream.Stderr);
        }
    }

    /// <summary>
    /// Starts the Docker daemon (DinD) inside the container via a shell script executed through
    /// docker exec, then polls until <c>docker info</c> succeeds or times out (60 s).
    /// Logs a warning (never throws) when the daemon fails to start so the agent can still operate
    /// for tasks that do not require Docker-in-Docker.
    /// </summary>
    protected async Task StartDindAsync(
        string containerId,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var script = BuildSimpleDindStartupScript();
        var exitCode = await ExecCommandAsync(
            containerId,
            ["/bin/sh", "-c", script],
            async (line, stream) => await onLogLine(line, stream),
            cancellationToken,
            workingDir: "/");

        if (exitCode != 0)
            await onLogLine("[WARN] dockerd startup script returned non-zero exit code", LogStream.Stderr);
    }

    /// <summary>
    /// Writes <c>/root/.config/act/actrc</c> inside the container, mapping all common
    /// Ubuntu platform labels (<c>ubuntu-latest</c>, <c>ubuntu-24.04</c>, <c>ubuntu-22.04</c>,
    /// <c>ubuntu-20.04</c>) to <paramref name="actRunnerImage"/>.
    ///
    /// This prevents <c>act</c> from prompting interactively for an image and ensures that
    /// CI workflow jobs run against an image that has the correct .NET SDK, Node.js, and
    /// other tooling installed. Best-effort — a failure is logged as a warning and does not
    /// abort the agent setup.
    /// </summary>
    /// <param name="containerId">Target container ID.</param>
    /// <param name="actRunnerImage">
    ///   Fully-qualified image reference to use as the act runner image, e.g.
    ///   <c>ghcr.io/issuepit/issuepit-act-runner:latest</c>.
    /// </param>
    /// <param name="onLogLine">Log callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task SetupActrcAsync(
        string containerId,
        string actRunnerImage,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        try
        {
            var script = BuildActrcSetupScript(actRunnerImage);
            await ExecCommandAsync(containerId, ["/bin/sh", "-c", script], onLogLine, cancellationToken, workingDir: "/");
            await onLogLine($"[INFO] actrc written (runner: {actRunnerImage})", LogStream.Stdout);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await onLogLine($"[WARN] actrc setup failed (non-fatal): {ex.Message}", LogStream.Stderr);
        }
    }

    /// <summary>
    /// Builds the shell script that writes <c>/root/.config/act/actrc</c> mapping each Ubuntu
    /// platform label to <paramref name="actRunnerImage"/>. Uses <c>printf '%b'</c> so the
    /// leading <c>-P</c> in the actrc content is not misinterpreted as a <c>printf</c> option
    /// by <c>/bin/sh</c> (dash).
    /// </summary>
    protected static string BuildActrcSetupScript(string actRunnerImage)
    {
        var platformLabels = new[] { "ubuntu-latest", "ubuntu-24.04", "ubuntu-22.04", "ubuntu-20.04" };
        // actrcBody: each line is "-P ubuntu-latest=<image>" joined with \n escape sequences.
        var actrcBody = string.Join("\\n", platformLabels.Select(label => $"-P {label}={actRunnerImage}"));
        return $"mkdir -p /root/.config/act && printf '%b' '{actrcBody}\\n' > /root/.config/act/actrc";
    }

    /// <summary>
    /// Builds the shell script that starts <c>dockerd</c> in the background and waits until
    /// the socket is ready. This is the simple version (no registry mirror, no apt-cache port)
    /// used by the agent runtime. The CI/CD runtime has its own extended version with registry
    /// mirror and iptables DNAT support.
    /// </summary>
    private static string BuildSimpleDindStartupScript()
    {
        var lines = new[]
        {
            // Fast-exit for minimal images (e.g. busybox used in E2E tests) that have neither
            // dockerd nor apt-get.  Without this guard the script would spin for up to 60 s
            // waiting for a dockerd that can never start, making agent tests unnecessarily slow.
            "command -v dockerd > /dev/null 2>&1 || command -v apt-get > /dev/null 2>&1 || { echo '[WARN] dockerd not available and no package manager found; skipping DinD setup' >&2; exit 0; }",
            // Install dockerd if not present (fallback for older images with docker-ce-cli only).
            "command -v dockerd > /dev/null 2>&1 || (apt-get update -qq 2>/dev/null && apt-get install -y --no-install-recommends docker.io 2>/dev/null)",
            "dockerd > /tmp/dockerd.log 2>&1 &",
            "TIMEOUT=60",
            "while [ $TIMEOUT -gt 0 ] && ! docker info > /dev/null 2>&1; do",
            "  sleep 1; TIMEOUT=$((TIMEOUT-1))",
            "done",
            "if docker info > /dev/null 2>&1; then",
            "  echo '[INFO] dockerd ready'",
            "else",
            "  echo '[WARN] dockerd did not start within timeout; continuing without DinD' >&2",
            "  cat /tmp/dockerd.log >&2 || true",
            "fi",
        };
        return string.Join('\n', lines);
    }

    /// <summary>
    /// Starts a local <c>dnsmasq</c> proxy inside the container to log DNS queries and, when
    /// <paramref name="disableInternet"/> is <c>true</c>, enforce a domain allowlist.
    /// Best-effort — skipped silently when dnsmasq is not installed.
    /// </summary>
    protected async Task SetupDnsProxyAsync(
        string containerId,
        bool disableInternet,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var script = BuildDnsProxyScript(disableInternet);
        try
        {
            await ExecCommandAsync(
                containerId,
                ["/bin/sh", "-c", script],
                async (line, stream) => await onLogLine(line, stream),
                cancellationToken,
                workingDir: "/");
        }
        catch (Exception ex)
        {
            await onLogLine($"[WARN] DNS proxy setup failed (non-fatal): {ex.Message}", LogStream.Stderr);
        }
    }

    /// <summary>
    /// Builds the shell script that sets up a local dnsmasq DNS proxy inside the container.
    /// Mirrors the entrypoint.sh step 4 logic.
    /// </summary>
    private static string BuildDnsProxyScript(bool disableInternet)
    {
        var allowedList = string.Join(' ', DefaultAllowedDomains);
        // language=bash
        return $$"""
                 if ! command -v dnsmasq > /dev/null 2>&1; then exit 0; fi
                 UPSTREAM_DNS=$(grep '^nameserver' /etc/resolv.conf 2>/dev/null | head -1 | awk '{print $2}')
                 if [ -z "${UPSTREAM_DNS}" ] || [ "${UPSTREAM_DNS}" = "127.0.0.1" ]; then exit 0; fi
                 DNSMASQ_ARGS="--no-hosts --no-resolv --log-queries --log-facility=- --listen-address=127.0.0.1 --bind-interfaces --pid-file=/tmp/dnsmasq.pid"
                 DISABLE_INTERNET={{(disableInternet ? "true" : "false")}}
                 if [ "${DISABLE_INTERNET}" = "true" ]; then
                   DNSMASQ_ARGS="${DNSMASQ_ARGS} --address=/#/0.0.0.0"
                   for DOMAIN in {{allowedList}}
                   do
                     DNSMASQ_ARGS="${DNSMASQ_ARGS} --server=/${DOMAIN}/${UPSTREAM_DNS}"
                   done
                 else
                   DNSMASQ_ARGS="${DNSMASQ_ARGS} --server=${UPSTREAM_DNS}"
                 fi
                 eval dnsmasq $DNSMASQ_ARGS
                 echo "nameserver 127.0.0.1" > /etc/resolv.conf
                 echo "[INFO] DNS proxy started (upstream: ${UPSTREAM_DNS}, DisableInternet=${DISABLE_INTERNET})"
                 """;
    }

    /// <summary>
    /// Writes <c>~/.config/opencode/config.json</c> inside the container with:
    /// <list type="bullet">
    ///   <item><c>autoupdate: false</c></item>
    ///   <item>Optional HTTP server <c>port</c> and <c>password</c></item>
    ///   <item>IssuePit MCP server (<paramref name="mcpUrl"/>)</item>
    ///   <item>Additional MCP servers from <paramref name="extraMcpJson"/></item>
    ///   <item>Agent configurations from <paramref name="agentsJson"/></item>
    ///   <item>Runtime plugins from <paramref name="pluginsJson"/></item>
    /// </list>
    /// This replaces the Python script that was previously run inside <c>entrypoint.sh</c>,
    /// allowing the entire opencode configuration to be built in C# and injected via the
    /// Docker archive API, with no shell escaping or Python dependency.
    /// </summary>
    protected async Task WriteOpencodeConfigAsync(
        string containerId,
        string? mcpUrl,
        string? agentsJson,
        string? extraMcpJson,
        int? port,
        string? password,
        string? pluginsJson,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        // Build the config object.
        var config = new Dictionary<string, object> { ["autoupdate"] = false };

        if (port.HasValue)
            config["port"] = port.Value;

        if (!string.IsNullOrWhiteSpace(password))
            config["password"] = password;

        // ── MCP section ──────────────────────────────────────────────────────
        var mcpSection = new Dictionary<string, object>();

        // mcpUrl is the base URL (e.g. http://host.docker.internal:5010).
        // The MCP Streamable HTTP endpoint is always at /mcp — append it here.
        var mcpEndpointUrl = string.IsNullOrWhiteSpace(mcpUrl)
            ? null
            : mcpUrl.TrimEnd('/') + "/mcp";

        if (mcpEndpointUrl is not null)
            mcpSection["issuepit"] = new Dictionary<string, object> { ["type"] = "remote", ["url"] = mcpEndpointUrl };

        if (!string.IsNullOrWhiteSpace(extraMcpJson))
        {
            try
            {
                using var extraDoc = JsonDocument.Parse(extraMcpJson);
                foreach (var server in extraDoc.RootElement.EnumerateArray())
                {
                    var name = server.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (string.IsNullOrEmpty(name)) continue;
                    var typeStr = server.TryGetProperty("type", out var t) ? t.GetString() : "remote";
                    // Map legacy "http"/"sse" to opencode-valid "remote".
                    typeStr = typeStr is "http" or "sse" ? "remote" : (typeStr ?? "remote");
                    var url = server.TryGetProperty("url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
                    var entry = new Dictionary<string, object> { ["type"] = typeStr, ["url"] = url };
                    if (server.TryGetProperty("headers", out var h) && h.ValueKind == JsonValueKind.Object)
                    {
                        var headers = new Dictionary<string, string>();
                        foreach (var hProp in h.EnumerateObject())
                            headers[hProp.Name] = hProp.Value.GetString() ?? string.Empty;
                        entry["headers"] = headers;
                    }
                    mcpSection[name!] = entry;
                }
            }
            catch (Exception ex)
            {
                await onLogLine($"[WARN] Could not parse extra MCP servers JSON: {ex.Message}", LogStream.Stderr);
            }
        }

        if (mcpSection.Count > 0)
            config["mcp"] = mcpSection;

        // ── Agent section ────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(agentsJson))
        {
            try
            {
                using var agentsDoc = JsonDocument.Parse(agentsJson);
                var agentMap = new Dictionary<string, object>();
                foreach (var a in agentsDoc.RootElement.EnumerateArray())
                {
                    var agentName = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (string.IsNullOrEmpty(agentName)) continue;
                    var agentKey = agentName!.ToLowerInvariant().Replace(' ', '-');
                    var prompt = a.TryGetProperty("prompt", out var p) ? p.GetString() ?? string.Empty : string.Empty;
                    var model = a.TryGetProperty("model", out var m) ? m.GetString() : null;
                    var agentType = a.TryGetProperty("agentType", out var at) ? at.GetString() : null;
                    var entry = new Dictionary<string, object> { ["prompt"] = prompt };
                    if (!string.IsNullOrEmpty(model)) entry["model"] = model!;
                    if (agentType is "primary" or "subagent" or "all") entry["mode"] = agentType!;
                    agentMap[agentKey] = entry;
                }
                if (agentMap.Count > 0)
                    config["agent"] = agentMap;
            }
            catch (Exception ex)
            {
                await onLogLine($"[WARN] Could not parse agents JSON: {ex.Message}", LogStream.Stderr);
            }
        }

        // ── Write config.json via tar injection ──────────────────────────────
        var configJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        var configBytes = Encoding.UTF8.GetBytes(configJson);

        // Ensure the target directory exists.
        await ExecCommandAsync(containerId,
            ["mkdir", "-p", "/root/.config/opencode"],
            (_, _) => Task.CompletedTask, cancellationToken, workingDir: "/");

        await InjectFileAsync(
            containerId,
            "/root/.config/opencode",
            "config.json",
            configBytes,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead,
            cancellationToken);

        await onLogLine("[INFO] opencode config written: /root/.config/opencode/config.json", LogStream.Stdout);
        if (mcpEndpointUrl is not null)
            await onLogLine($"[DEBUG] MCP URL in config : {mcpEndpointUrl}", LogStream.Stdout);

        // ── Write runtime plugins ────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(pluginsJson))
        {
            try
            {
                using var pluginsDoc = JsonDocument.Parse(pluginsJson);
                await ExecCommandAsync(containerId,
                    ["mkdir", "-p", "/root/.config/opencode/plugins"],
                    (_, _) => Task.CompletedTask, cancellationToken, workingDir: "/");

                foreach (var plugin in pluginsDoc.RootElement.EnumerateArray())
                {
                    var name = plugin.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var content = plugin.TryGetProperty("content", out var c) ? c.GetString() : null;
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(content)) continue;
                    if (!name!.EndsWith(".js") && !name.EndsWith(".ts"))
                        name += ".js";
                    var pluginBytes = Encoding.UTF8.GetBytes(content!);
                    await InjectFileAsync(
                        containerId,
                        "/root/.config/opencode/plugins",
                        name,
                        pluginBytes,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead,
                        cancellationToken);
                    await onLogLine($"[INFO] Runtime plugin written: {name}", LogStream.Stdout);
                }
            }
            catch (Exception ex)
            {
                await onLogLine($"[WARN] Could not write runtime plugins: {ex.Message}", LogStream.Stderr);
            }
        }
    }
    /// <summary>Returns <c>true</c> when <paramref name="s"/> contains any of the given substrings (ordinal, case-insensitive).</summary>
    private static bool ContainsAny(string s, params string[] values) =>
        values.Any(v => s.Contains(v, StringComparison.OrdinalIgnoreCase));
}
