using System.Reflection;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.Core.Runners;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>
/// Runs the agent inside a local Docker container with Docker-in-Docker (DinD) support.
///
/// When <see cref="Agent.RunnerType"/> is set, the container is kept alive with
/// <c>sleep infinity</c> after entrypoint setup, and all agent commands are driven via
/// <c>docker exec</c> from C#. This keeps the same container — and therefore the same
/// opencode session files on disk — across the initial run and any subsequent fix runs
/// (uncommitted-changes or CI/CD failure fixes).
///
/// When <see cref="Agent.RunnerType"/> is null (legacy mode), the container runs its
/// default CMD and is waited on as before.
/// </summary>
public class DockerAgentRuntime(ILogger<DockerAgentRuntime> logger, DockerClient dockerClient, IConfiguration configuration)
    : IExecCapableRuntime
{
    // Docker image used to run agents. Uses the IssuePit helper-opencode-act image which includes
    // the opencode CLI, Docker Engine (DinD), act, .NET SDK, Node.js, and Playwright.
    // Overridden by agent.DockerImage when set.
    private const string DefaultDockerImage = "ghcr.io/issuepit/issuepit-helper-opencode-act:main-dotnet10-node24";

    /// <summary>Read buffer size for streaming container log output. 80 KiB matches the Docker SDK convention.</summary>
    private const int LogBufferSize = 81920;

    // Special log-line prefixes emitted by this class so IssueWorker can parse them.
    internal const string GitCommitShaMarker = "[ISSUEPIT:GIT_COMMIT_SHA]=";
    internal const string GitBranchMarker = "[ISSUEPIT:GIT_BRANCH]=";
    internal const string HasUncommittedChangesMarker = "[ISSUEPIT:HAS_UNCOMMITTED_CHANGES]=";
    internal const string OpenCodeSessionIdMarker = "[ISSUEPIT:OPENCODE_SESSION_ID]=";

    private static string AppVersion =>
        Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

    // ──────────────────────────────────────────────────────────────────────────
    // IAgentRuntime / IExecCapableRuntime implementation
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<string> LaunchAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        RuntimeConfiguration? runtimeConfig,
        GitRepository? gitRepository,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var image = !string.IsNullOrWhiteSpace(agent.DockerImage)
            ? agent.DockerImage
            : DefaultDockerImage;

        // Emit verbose diagnostics as the first log lines so they appear in the session log output.
        await onLogLine($"[DEBUG] Runner machine : {Environment.MachineName}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Runtime        : Docker", LogStream.Stdout);
        await onLogLine($"[DEBUG] IssuePit ver   : {AppVersion}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Agent          : {agent.Name} ({agent.Id})", LogStream.Stdout);
        await onLogLine($"[DEBUG] Issue          : #{issue.Number} {issue.Title}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Session        : {session.Id}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Docker image   : {image}", LogStream.Stdout);
        if (agent.RunnerType is not null)
            await onLogLine($"[DEBUG] Runner type    : {agent.RunnerType}", LogStream.Stdout);
        if (!string.IsNullOrWhiteSpace(agent.Model))
            await onLogLine($"[DEBUG] Model          : {agent.Model}", LogStream.Stdout);
        await onLogLine($"[DEBUG] DinD           : isolated (Privileged=true, in-container dockerd)", LogStream.Stdout);
        if (agent.DisableInternet)
            await onLogLine($"[DEBUG] Internet       : restricted", LogStream.Stdout);
        if (session.KeepContainer)
            await onLogLine($"[DEBUG] Keep container : true (container will not be removed on exit)", LogStream.Stdout);
        if (gitRepository is not null)
            await onLogLine($"[DEBUG] Git remote     : {gitRepository.RemoteUrl}", LogStream.Stdout);

        // Verify Docker daemon is reachable and log its version.
        try
        {
            var dockerVersion = await dockerClient.System.GetVersionAsync(cancellationToken);
            await onLogLine($"[DEBUG] Docker version : {dockerVersion.Version} (API {dockerVersion.APIVersion})", LogStream.Stdout);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Cannot connect to the Docker daemon. Ensure Docker is running and the socket is accessible " +
                $"(inner: {ex.Message})", ex);
        }

        // Step 1: Pull the container image explicitly before creating the container.
        var pullStart = DateTime.UtcNow;
        await onLogLine($"[DEBUG] Pull started   : {pullStart:u}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Pulling image  : {image}", LogStream.Stdout);
        await PullImageAsync(image, cancellationToken);
        var pullDuration = (DateTime.UtcNow - pullStart).TotalSeconds;
        await onLogLine($"[DEBUG] Pull finished  : {DateTime.UtcNow:u} (took {pullDuration:F1}s)", LogStream.Stdout);

        // Step 2: Build environment including git repo info so the container can clone the repo on startup.
        var env = AgentEnvironmentBuilder.Build(session, agent, issue, credentials, gitRepository);

        // Step 3: Determine whether to use the exec-based flow (RunnerType set) or the legacy flow.
        //
        // Exec flow  — container CMD = "sleep infinity"; entrypoint does setup and keeps container alive;
        //              C# execs the agent tool and all post-run steps (git check, markers, push).
        // Legacy flow — container CMD from entrypoint default; wait for container to exit (old behaviour).
        var runnerArgs = RunnerCommandBuilder.BuildArgsList(agent, issue);
        var useExecFlow = runnerArgs.Count > 0;

        if (useExecFlow)
        {
            // Log the CLI command (e.g. "opencode run --model ...") without the task text.
            var cmdDisplay = string.Join(" ", runnerArgs.Take(runnerArgs.Count - 1));
            await onLogLine($"[DEBUG] Runner cmd     : {cmdDisplay}", LogStream.Stdout);
        }

        // Log the task prompt that will be passed to the agent so it is always visible in the logs.
        var taskPrompt = RunnerCommandBuilder.BuildTaskPrompt(issue);
        await onLogLine($"[DEBUG] Task prompt    :", LogStream.Stdout);
        foreach (var promptLine in taskPrompt.Split('\n'))
            await onLogLine($"[DEBUG]   {promptLine}", LogStream.Stdout);

        // Step 4: Configure DNS-based firewall when internet access should be restricted.
        var dns = agent.DisableInternet ? GetRestrictedDns() : null;

        logger.LogInformation("Creating Docker container from image {Image} for agent {AgentId} (DisableInternet={DisableInternet}, ExecFlow={UseExecFlow})",
            image, agent.Id, agent.DisableInternet, useExecFlow);

        var hostConfig = new HostConfig
        {
            Privileged = true,
            // Exec flow manages its own lifecycle (stopped by StopContainerAsync after all work is done).
            // Legacy flow uses AutoRemove as before.
            AutoRemove = useExecFlow ? false : !session.KeepContainer,
        };

        if (dns is not null)
            hostConfig.DNS = dns;

        var createParams = new CreateContainerParameters
        {
            Image = image,
            Env = env,
            // Exec flow: keep container alive; legacy flow: run container's default CMD.
            Cmd = useExecFlow ? ["sleep", "infinity"] : null,
            HostConfig = hostConfig,
            Labels = new Dictionary<string, string>
            {
                ["issuepit.session-id"] = session.Id.ToString(),
                ["issuepit.issue-id"] = issue.Id.ToString(),
                ["issuepit.agent-id"] = agent.Id.ToString(),
            },
        };

        // Step 5: Create and start the container.
        var container = await dockerClient.Containers.CreateContainerAsync(
            createParams, cancellationToken);

        await dockerClient.Containers.StartContainerAsync(
            container.ID, new ContainerStartParameters(), cancellationToken);

        var shortContainerId = container.ID[..Math.Min(12, container.ID.Length)];
        await onLogLine($"[DEBUG] Container ID   : {shortContainerId}", LogStream.Stdout);

        logger.LogInformation("Started Docker container {ContainerId} for agent session {SessionId} (ExecFlow={UseExecFlow})",
            container.ID, session.Id, useExecFlow);

        if (!useExecFlow)
        {
            // ── Legacy flow ──────────────────────────────────────────────────────
            // Stream logs and wait for the container to exit naturally.
            var logStreamTask = StreamContainerLogsAsync(container.ID, onLogLine, cancellationToken);
            var waitResponse = await dockerClient.Containers.WaitContainerAsync(container.ID, cancellationToken);
            await logStreamTask;

            if (waitResponse.StatusCode != 0)
                throw new Exception(
                    $"Agent container exited with code {waitResponse.StatusCode} " +
                    $"(image: {image}, session: {session.Id})");

            return container.ID;
        }

        // ── Exec flow ────────────────────────────────────────────────────────
        // The container is alive (running `sleep infinity`). Drive all agent work via exec.
        try
        {
            // Step 6: Execute the agent tool via docker exec.
            var agentExitCode = await ExecCommandAsync(container.ID, runnerArgs, onLogLine, cancellationToken);

            // Step 7: Capture the opencode session ID for --fork on subsequent fix runs.
            // NOTE: opencode run --fork <session-id> will continue from the same session and retain
            // full conversation context. The same container already gives the agent access to the
            // git workspace as modified by the first run. --fork will be wired up once opencode
            // supports the flag in non-interactive (opencode run) mode.
            if (agent.RunnerType == RunnerType.OpenCode)
            {
                try { await CaptureOpenCodeSessionIdAsync(container.ID, onLogLine, cancellationToken); }
                catch (Exception ex)
                {
                    await onLogLine($"[WARN] opencode session list failed: {ex.Message}", LogStream.Stderr);
                }
            }

            // Step 8: Check git state and emit markers so IssueWorker can trigger CI/CD.
            if (gitRepository is not null)
            {
                try
                {
                    await CheckAndEmitUncommittedChangesAsync(container.ID, onLogLine, cancellationToken);
                    await EmitGitMarkersAsync(container.ID, onLogLine, cancellationToken);
                }
                catch (Exception ex)
                {
                    await onLogLine($"[WARN] Git state check failed: {ex.Message}", LogStream.Stderr);
                }
            }

            if (agentExitCode != 0)
                throw new Exception(
                    $"Agent exited with code {agentExitCode} (image: {image}, session: {session.Id})");

            // Return the container ID — the container is still alive for fix runs.
            return container.ID;
        }
        catch
        {
            // Clean up the container on failure unless KeepContainer is set (for debugging).
            if (!session.KeepContainer)
                await TryStopAndRemoveContainerAsync(container.ID);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<(string? CommitSha, string? BranchName)> ExecFixInContainerAsync(
        string containerId,
        string? openCodeSessionId,
        AgentSession parentSession,
        Agent agent,
        Issue fixIssue,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        string? fixCommitSha = null;
        string? fixBranchName = null;

        // Intercept git markers before prefixing with [fix] so we can return them to the caller.
        Task onFixLogLine(string line, LogStream stream)
        {
            if (line.StartsWith(GitCommitShaMarker, StringComparison.Ordinal))
                fixCommitSha = line[GitCommitShaMarker.Length..].Trim();
            else if (line.StartsWith(GitBranchMarker, StringComparison.Ordinal))
                fixBranchName = line[GitBranchMarker.Length..].Trim();
            return onLogLine($"[fix] {line}", stream);
        }

        var runnerArgs = RunnerCommandBuilder.BuildArgsList(agent, fixIssue, forkSessionId: openCodeSessionId);
        if (runnerArgs.Count > 0)
        {
            var shortId = containerId[..Math.Min(12, containerId.Length)];
            var forkInfo = openCodeSessionId is not null ? $" (--session {openCodeSessionId[..Math.Min(8, openCodeSessionId.Length)]} --fork)" : string.Empty;
            await onLogLine($"[INFO] Exec fix run in container {shortId}{forkInfo}…", LogStream.Stdout);
            var exitCode = await ExecCommandAsync(containerId, runnerArgs, onFixLogLine, cancellationToken);
            if (exitCode != 0)
                await onLogLine($"[WARN] Fix agent exited with code {exitCode}", LogStream.Stderr);
        }

        // Emit git markers so the caller can capture the updated commit SHA and branch.
        try { await EmitGitMarkersAsync(containerId, onFixLogLine, cancellationToken); }
        catch (Exception ex) { await onFixLogLine($"[WARN] Git marker emission failed: {ex.Message}", LogStream.Stderr); }

        return (fixCommitSha, fixBranchName);
    }

    /// <inheritdoc/>
    public async Task StopContainerAsync(string containerId, bool remove, CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping agent container {ContainerId} (Remove={Remove})", containerId, remove);
        try
        {
            await dockerClient.Containers.StopContainerAsync(
                containerId, new ContainerStopParameters { WaitBeforeKillSeconds = 5 }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to stop container {ContainerId}", containerId);
        }

        if (!remove) return;

        try
        {
            await dockerClient.Containers.RemoveContainerAsync(
                containerId, new ContainerRemoveParameters(), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove container {ContainerId}", containerId);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Docker exec helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes <paramref name="cmd"/> inside a running container via docker exec.
    /// Streams output to <paramref name="onLogLine"/> and returns the process exit code.
    /// </summary>
    private async Task<long> ExecCommandAsync(
        string containerId,
        IReadOnlyList<string> cmd,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var execCreate = await dockerClient.Exec.CreateContainerExecAsync(containerId,
            new ContainerExecCreateParameters
            {
                Cmd = cmd.ToList(),
                AttachStdout = true,
                AttachStderr = true,
                WorkingDir = "/workspace",
            }, cancellationToken);

        using var stream = await dockerClient.Exec.StartContainerExecAsync(
            execCreate.ID, new ContainerExecStartParameters { Detach = false }, cancellationToken);

        await ReadMultiplexedStreamAsync(stream, onLogLine, cancellationToken);

        var inspect = await dockerClient.Exec.InspectContainerExecAsync(execCreate.ID, cancellationToken);
        return inspect.ExitCode ?? 0;
    }

    /// <summary>
    /// Executes <paramref name="cmd"/> inside a running container and returns the combined output
    /// as a trimmed string. Output is not forwarded to any log sink.
    /// </summary>
    private async Task<string> ExecReadOutputAsync(
        string containerId,
        IReadOnlyList<string> cmd,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        await ExecCommandAsync(containerId, cmd,
            (line, _) => { sb.AppendLine(line); return Task.CompletedTask; },
            cancellationToken);
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Runs <c>opencode session list</c> and emits a <c>[ISSUEPIT:OPENCODE_SESSION_ID]</c> marker
    /// with the most recently started session ID. IssueWorker captures this for <c>--fork</c>.
    /// </summary>
    private async Task CaptureOpenCodeSessionIdAsync(
        string containerId,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        await onLogLine("[entrypoint] opencode session list:", LogStream.Stdout);

        string? lastSessionId = null;
        await ExecCommandAsync(containerId, ["opencode", "session", "list"],
            async (line, stream) =>
            {
                await onLogLine($"[entrypoint]   {line}", stream);
                // Session list format: <id>  <date>  <title> — first whitespace-delimited token is the ID.
                // opencode lists sessions newest-first, so the first non-empty token is the most recent session.
                if (lastSessionId is null)
                {
                    var tokenEnd = line.IndexOfAny([' ', '\t']);
                    var token = (tokenEnd > 0 ? line[..tokenEnd] : line).Trim();
                    if (!string.IsNullOrWhiteSpace(token))
                        lastSessionId = token;
                }
            }, cancellationToken);

        if (!string.IsNullOrWhiteSpace(lastSessionId))
            await onLogLine($"{OpenCodeSessionIdMarker}{lastSessionId}", LogStream.Stdout);
    }

    /// <summary>
    /// Runs <c>git status --porcelain</c> in the workspace and emits
    /// <c>[ISSUEPIT:HAS_UNCOMMITTED_CHANGES]=true</c> when any uncommitted files are found.
    /// </summary>
    private async Task CheckAndEmitUncommittedChangesAsync(
        string containerId,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var statusOutput = await ExecReadOutputAsync(
            containerId, ["git", "status", "--porcelain"], cancellationToken);

        if (string.IsNullOrWhiteSpace(statusOutput)) return;

        await onLogLine("[entrypoint] WARNING: uncommitted changes found after agent run", LogStream.Stdout);
        foreach (var line in statusOutput.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).Take(20))
            await onLogLine($"[entrypoint]   {line}", LogStream.Stdout);
        await onLogLine($"{HasUncommittedChangesMarker}true", LogStream.Stdout);
    }

    /// <summary>
    /// Pushes the current branch to origin (allowed to fail), then emits
    /// <c>[ISSUEPIT:GIT_COMMIT_SHA]</c> and <c>[ISSUEPIT:GIT_BRANCH]</c> markers.
    /// </summary>
    private async Task EmitGitMarkersAsync(
        string containerId,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var branch = await ExecReadOutputAsync(
            containerId, ["git", "branch", "--show-current"], cancellationToken);
        var commitSha = await ExecReadOutputAsync(
            containerId, ["git", "rev-parse", "HEAD"], cancellationToken);

        // Push is allowed to fail — credentials may not be configured yet.
        if (!string.IsNullOrWhiteSpace(branch))
        {
            await onLogLine($"[entrypoint] Pushing branch '{branch}' to origin…", LogStream.Stdout);
            var pushExit = await ExecCommandAsync(containerId, ["git", "push", "origin", branch],
                async (line, stream) => await onLogLine($"[entrypoint] {line}", stream),
                cancellationToken);
            if (pushExit != 0)
                await onLogLine(
                    "[entrypoint] Push failed (allowed — credentials may not be configured or push was rejected)",
                    LogStream.Stdout);
        }

        if (!string.IsNullOrWhiteSpace(commitSha))
            await onLogLine($"{GitCommitShaMarker}{commitSha}", LogStream.Stdout);
        if (!string.IsNullOrWhiteSpace(branch))
            await onLogLine($"{GitBranchMarker}{branch}", LogStream.Stdout);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Container log streaming
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Streams the container's stdout/stderr to <paramref name="onLogLine"/> and blocks until the container exits.
    /// Used by the legacy (non-exec) flow only.
    /// </summary>
    private async Task StreamContainerLogsAsync(
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

        using var stream = await dockerClient.Containers.GetContainerLogsAsync(
            containerId, logsParams, cancellationToken);

        await ReadMultiplexedStreamAsync(stream, onLogLine, cancellationToken);
    }

    /// <summary>
    /// Reads a <see cref="MultiplexedStream"/> line by line and forwards each line to
    /// <paramref name="onLogLine"/>. Shared between container log streaming and docker exec output.
    /// </summary>
    private static async Task ReadMultiplexedStreamAsync(
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

            // All but the last element are complete lines.
            for (var i = 0; i < lines.Length - 1; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (!string.IsNullOrEmpty(line))
                    await onLogLine(line, lastTarget);
            }

            // Keep the trailing (possibly incomplete) fragment for the next iteration.
            remainder = lines[^1];
        }

        // Flush any remaining content after EOF.
        var flushed = remainder.TrimEnd('\r');
        if (!string.IsNullOrEmpty(flushed))
            await onLogLine(flushed, lastTarget);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Docker image and DNS helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Explicitly pulls the Docker image, ensuring the latest version is available before container creation.</summary>
    private async Task PullImageAsync(string image, CancellationToken cancellationToken)
    {
        logger.LogInformation("Pulling Docker image {Image}", image);

        var lastSlash = image.LastIndexOf('/');
        var tagColonIndex = image.IndexOf(':', lastSlash + 1);
        var (fromImage, tag) = tagColonIndex >= 0
            ? (image[..tagColonIndex], image[(tagColonIndex + 1)..])
            : (image, "latest");

        await dockerClient.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = fromImage, Tag = tag },
            authConfig: null!,
            new Progress<JSONMessage>(msg =>
            {
                if (!string.IsNullOrEmpty(msg.Status))
                    logger.LogDebug("Pull {Image}: {Status}", image, msg.Status);
            }),
            cancellationToken);

        logger.LogInformation("Docker image {Image} is ready", image);
    }

    /// <summary>Returns the list of DNS server addresses to use when internet access is restricted.</summary>
    private IList<string>? GetRestrictedDns()
    {
        var dnsServer = configuration["Execution:RestrictedDnsServer"];
        if (string.IsNullOrWhiteSpace(dnsServer))
        {
            logger.LogWarning(
                "Agent has DisableInternet=true but 'Execution:RestrictedDnsServer' is not configured. Internet access will not be restricted.");
            return null;
        }
        return [dnsServer];
    }

    /// <summary>Best-effort stop + remove of a container. Used for cleanup on failure paths.</summary>
    private async Task TryStopAndRemoveContainerAsync(string containerId)
    {
        try
        {
            await dockerClient.Containers.StopContainerAsync(
                containerId, new ContainerStopParameters { WaitBeforeKillSeconds = 3 }, CancellationToken.None);
            await dockerClient.Containers.RemoveContainerAsync(
                containerId, new ContainerRemoveParameters(), CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean up container {ContainerId} after error", containerId);
        }
    }
}
