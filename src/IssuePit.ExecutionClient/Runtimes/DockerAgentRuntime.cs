using System.Formats.Tar;
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
public class DockerAgentRuntime(
    ILogger<DockerAgentRuntime> logger,
    DockerClient dockerClient,
    IConfiguration configuration,
    IAgentHttpApi agentHttpApi)
    : IExecCapableRuntime
{
    // Docker image used to run agents. Uses the IssuePit helper-opencode-act image which includes
    // the opencode CLI, Docker Engine (DinD), act, .NET SDK, Node.js, and Playwright.
    // Overridden by agent.DockerImage when set.
    private const string DefaultDockerImage = "ghcr.io/issuepit/issuepit-helper-opencode-act:main-dotnet10-node24";

    /// <summary>Read buffer size for streaming container log output. 80 KiB matches the Docker SDK convention.</summary>
    private const int LogBufferSize = 81920;

    /// <summary>Length of the hex suffix appended to agent container names for uniqueness.</summary>
    private const int ContainerNameSuffixLength = 10;

    // Special log-line prefixes emitted by this class so IssueWorker can parse them.
    internal const string GitCommitShaMarker = "[ISSUEPIT:GIT_COMMIT_SHA]=";
    internal const string GitBranchMarker = "[ISSUEPIT:GIT_BRANCH]=";
    internal const string HasUncommittedChangesMarker = "[ISSUEPIT:HAS_UNCOMMITTED_CHANGES]=";
    internal const string OpenCodeSessionIdMarker = "[ISSUEPIT:OPENCODE_SESSION_ID]=";
    /// <summary>Emitted when <c>git push</c> fails; IssueWorker uses this to trigger a .git archive upload.</summary>
    internal const string GitPushFailedMarker = "[ISSUEPIT:GIT_PUSH_FAILED]=true";
    /// <summary>
    /// Emitted when the agent is running in HTTP server mode; carries the URL of the agent's web UI.
    /// IssueWorker captures this and persists it on <see cref="AgentSession.ServerWebUiUrl"/>.
    /// </summary>
    internal const string ServerWebUiUrlMarker = "[ISSUEPIT:SERVER_WEB_UI_URL]=";

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
        if (agent.UseHttpServer)
            await onLogLine($"[DEBUG] Server mode    : HTTP (opencode server API)", LogStream.Stdout);
        await onLogLine($"[DEBUG] DinD           : isolated (Privileged=true, in-container dockerd)", LogStream.Stdout);
        if (agent.DisableInternet)
            await onLogLine($"[DEBUG] Internet       : restricted", LogStream.Stdout);
        if (session.KeepContainer)
            await onLogLine($"[DEBUG] Keep container : true (container will not be removed on exit)", LogStream.Stdout);
        if (gitRepository is not null)
        {
            await onLogLine($"[DEBUG] Git remote     : {gitRepository.RemoteUrl} ({gitRepository.Mode})", LogStream.Stdout);
            // Determine the branch the container will check out: issue.GitBranch takes precedence
            // (feature branch for this issue), otherwise falls back to the repo's default branch.
            var effectiveBranch = !string.IsNullOrWhiteSpace(issue.GitBranch)
                ? issue.GitBranch
                : gitRepository.DefaultBranch;
            if (!string.IsNullOrWhiteSpace(effectiveBranch))
                await onLogLine($"[DEBUG] Git branch     : {effectiveBranch}", LogStream.Stdout);

            // Validate that we have a branch to clone. When no feature branch is set on the issue
            // the entrypoint uses DefaultBranch as the base — if that is also empty there is
            // nothing to clone. Fail here with a clear message rather than letting the container
            // start and exit with a cryptic git error.
            if (string.IsNullOrWhiteSpace(issue.GitBranch) && string.IsNullOrWhiteSpace(gitRepository.DefaultBranch))
                throw new InvalidOperationException(
                    $"GitRepository '{gitRepository.RemoteUrl}' has no DefaultBranch configured and the issue has no GitBranch set. " +
                    "Set the default branch in the project's git repository settings before running an agent.");
        }
        if (agent.ChildAgents.Count > 0)
        {
            await onLogLine($"[DEBUG] Child agents   : {agent.ChildAgents.Count}", LogStream.Stdout);
            foreach (var child in agent.ChildAgents)
                await onLogLine($"[DEBUG]   - {child.Name} ({child.Id})", LogStream.Stdout);
        }
        
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
        // Replace localhost/127.0.0.1 with host.docker.internal so containers can reach the host's services
        // (e.g. the IssuePit MCP server). The container host-gateway ExtraHost added below makes
        // host.docker.internal resolvable both on Linux (via host-gateway) and Docker Desktop.
        //
        // The Aspire DCP proxy is disabled for the MCP server (IsProxied=false in AppHost) so
        // McpServer:BaseUrl already contains the direct target port URL (http://localhost:{T}).
        // ToDockerHostUrl converts localhost → host.docker.internal so the container reaches T
        // via 172.17.0.1:{T}. The MCP server binds to 0.0.0.0:{T} via ListenAnyIP in Program.cs.
        var issuePitMcpUrl = ToDockerHostUrl(configuration["McpServer:BaseUrl"]);
        var env = AgentEnvironmentBuilder.Build(session, agent, issue, credentials, gitRepository, issuePitMcpUrl);
        if (!string.IsNullOrWhiteSpace(issuePitMcpUrl))
            await onLogLine($"[DEBUG] IssuePit MCP   : {issuePitMcpUrl}", LogStream.Stdout);

        // Inject the HTTP server password so opencode can use it for authentication when UseHttpServer=true.
        if (agent.UseHttpServer && !string.IsNullOrWhiteSpace(agent.HttpServerPassword))
            env.Add($"OPENCODE_PASSWORD={agent.HttpServerPassword}");

        // Explicitly set the opencode server port so the entrypoint writes it to config.json.
        // Without this, opencode uses its built-in default (4096), but setting it explicitly
        // ensures the port in config.json matches the container port binding.
        if (agent.UseHttpServer && agent.RunnerType == RunnerType.OpenCode)
            env.Add($"OPENCODE_PORT={OpenCodeHttpApi.DefaultPort}");

        // Step 3: Determine the execution mode.
        //
        // HTTP server mode — container CMD = "opencode" (starts the HTTP server); C# uses the REST
        //                    API to create sessions, send tasks, and poll for results. Supports
        //                    parallel tasks on the same server. The server's web UI URL is emitted
        //                    as a [ISSUEPIT:SERVER_WEB_UI_URL]= marker for IssueWorker.
        // Exec flow        — container CMD = "sleep infinity"; C# drives all agent commands via
        //                    docker exec. Keeps the same opencode session files across fix runs.
        // Legacy flow      — container CMD from entrypoint default; wait for container to exit.
        var useHttpServerMode = agent.UseHttpServer && agent.RunnerType == RunnerType.OpenCode;

        var comments = issue.Comments.Count > 0 ? (IReadOnlyList<IssueComment>)issue.Comments : null;
        if (comments is not null)
            await onLogLine($"[DEBUG] Comments       : {comments.Count} comment(s) included in prompt", LogStream.Stdout);

        // Build CLI args for exec flow (not used in HTTP server mode).
        // When the session has a preserved previous opencode session ID (and the DB was injected),
        // pass it as --session <id> so opencode continues from the preserved conversation.
        var runnerArgs = useHttpServerMode ? [] : RunnerCommandBuilder.BuildArgsList(
            agent, issue,
            continueSessionId: session.PreviousOpenCodeSessionId,
            comments: comments);
        var useExecFlow = !useHttpServerMode && runnerArgs.Count > 0;

        if (useExecFlow)
        {
            // Log the CLI command (e.g. "opencode run --model ...") without the task text.
            var cmdDisplay = string.Join(" ", runnerArgs.Take(runnerArgs.Count - 1));
            await onLogLine($"[DEBUG] Runner cmd     : {cmdDisplay}", LogStream.Stdout);
        }

        // Log the task prompt that will be passed to the agent so it is always visible in the logs.
        var taskPrompt = RunnerCommandBuilder.BuildTaskPrompt(issue, comments);
        await onLogLine($"[DEBUG] Task prompt    :", LogStream.Stdout);
        foreach (var promptLine in taskPrompt.Split('\n'))
            await onLogLine($"[DEBUG]   {promptLine}", LogStream.Stdout);

        // Step 4: Configure DNS-based firewall when internet access should be restricted.
        var dns = agent.DisableInternet ? GetRestrictedDns() : null;

        logger.LogInformation(
            "Creating Docker container from image {Image} for agent {AgentId} (DisableInternet={DisableInternet}, ExecFlow={UseExecFlow}, HttpServer={UseHttpServer})",
            image, agent.Id, agent.DisableInternet, useExecFlow, useHttpServerMode);

        var hostConfig = new HostConfig
        {
            Privileged = true,
            // Never auto-remove: both exec flow and legacy flow manage container lifetime explicitly.
            // AutoRemove races with WaitContainerAsync / StreamContainerLogsAsync — the container can
            // be removed before logs are fully streamed, producing NotFound errors. Instead, we always
            // remove the container ourselves after all log capture is complete (see below).
            AutoRemove = false,
            // Make host.docker.internal resolve to the Docker host gateway so containers can call
            // the IssuePit MCP server and other host services. "host-gateway" is the Docker-native
            // special value that resolves to the correct host IP on both Linux and Docker Desktop.
            ExtraHosts = ["host.docker.internal:host-gateway"],
        };

        if (dns is not null)
            hostConfig.DNS = dns;

        // Expose the opencode HTTP server port to the host when running in HTTP server mode.
        // An empty HostPort causes Docker to assign a random available host port.
        Dictionary<string, EmptyStruct>? containerExposedPorts = null;
        if (useHttpServerMode)
        {
            var containerPort = $"{OpenCodeHttpApi.DefaultPort}/tcp";
            containerExposedPorts = new Dictionary<string, EmptyStruct> { { containerPort, new EmptyStruct() } };
            hostConfig.PortBindings = new Dictionary<string, IList<PortBinding>>
            {
                { containerPort, [new PortBinding { HostPort = "" }] },
            };
            await onLogLine($"[DEBUG] HTTP server    : port {OpenCodeHttpApi.DefaultPort} (random host port)", LogStream.Stdout);
        }

        // Container CMD:
        //   - HTTP server mode: "opencode" (no run subcommand — starts the HTTP server)
        //   - Exec flow:        "sleep infinity" (entrypoint keeps container alive for docker exec)
        //   - Legacy flow:      null → use image's default CMD (or session.CustomCmd if set)
        IList<string>? containerCmd = useHttpServerMode
            ? ["opencode"]
            : useExecFlow
                ? ["sleep", "infinity"]
                : (session.CustomCmd?.Length > 0 ? session.CustomCmd : null);

        // Generate a unique container name from the session ID, similar to how CI/CD runner
        // uses --container-name-suffix. This makes agent containers identifiable by session
        // and prevents name collisions across parallel runs.
        var containerName = $"issuepit-agent-{session.Id:N}"[..(32 + ContainerNameSuffixLength)];

        var createParams = new CreateContainerParameters
        {
            Name = containerName,
            Image = image,
            Env = env,
            Cmd = containerCmd,
            ExposedPorts = containerExposedPorts,
            HostConfig = hostConfig,
            Labels = new Dictionary<string, string>
            {
                ["issuepit.session-id"] = session.Id.ToString(),
                ["issuepit.issue-id"] = issue.Id.ToString(),
                ["issuepit.agent-id"] = agent.Id.ToString(),
            },
        };

        // Step 5: Create the container, inject the latest entrypoint script, then start it.
        // Injecting entrypoint.sh from the embedded resource ensures the container always uses
        // the version that shipped with this execution client binary, keeping them in sync even
        // when the Docker image was built with an older entrypoint.
        var container = await dockerClient.Containers.CreateContainerAsync(
            createParams, cancellationToken);

        try
        {
            await InjectEntrypointAsync(container.ID, cancellationToken);
            await onLogLine("[DEBUG] Entrypoint     : injected from execution client binary", LogStream.Stdout);
        }
        catch (Exception ex)
        {
            // Injection failure is non-fatal — the container's baked-in entrypoint will be used.
            logger.LogWarning(ex, "Failed to inject entrypoint into container {ContainerId}; using baked-in version", container.ID);
            await onLogLine($"[WARN] Entrypoint injection failed: {ex.Message} (using baked-in version)", LogStream.Stderr);
        }

        await dockerClient.Containers.StartContainerAsync(
            container.ID, new ContainerStartParameters(), cancellationToken);

        var shortContainerId = container.ID[..Math.Min(12, container.ID.Length)];
        await onLogLine($"[DEBUG] Container ID   : {shortContainerId}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Container name : {containerName}", LogStream.Stdout);

        logger.LogInformation("Started Docker container {ContainerId} (name: {ContainerName}) for agent session {SessionId} (ExecFlow={UseExecFlow})",
            container.ID, containerName, session.Id, useExecFlow);

        // Inject the preserved opencode DB snapshot from the previous session so the agent can
        // continue the previous conversation. The DB is injected after the container starts (so
        // the entrypoint can create the home directory) but before opencode runs.
        if (session.PreviousOpenCodeDbTar is { Length: > 0 })
        {
            try
            {
                await onLogLine($"[INFO] Injecting previous opencode DB snapshot (session: {session.PreviousOpenCodeSessionId ?? "unknown"})…", LogStream.Stdout);
                using var dbStream = new MemoryStream(session.PreviousOpenCodeDbTar);
                // The tar archive path must already contain the correct relative directory
                // so Docker extracts the file to /root/.local/share/opencode/opencode.db.
                await dockerClient.Containers.ExtractArchiveToContainerAsync(
                    container.ID,
                    new CopyToContainerParameters { Path = "/" },
                    dbStream,
                    cancellationToken);
                await onLogLine("[INFO] opencode DB snapshot injected — continuing from previous session", LogStream.Stdout);
            }
            catch (Exception ex)
            {
                await onLogLine($"[WARN] Failed to inject opencode DB snapshot: {ex.Message} — starting fresh session", LogStream.Stderr);
                logger.LogWarning(ex, "Failed to inject opencode DB into container {ContainerId}", container.ID);
            }
        }

        // Emit previous session ID so it appears in the logs for traceability.
        if (!string.IsNullOrWhiteSpace(session.PreviousOpenCodeSessionId))
            await onLogLine($"[INFO] Continuing from previous opencode session: {session.PreviousOpenCodeSessionId}", LogStream.Stdout);

        if (!useExecFlow && !useHttpServerMode)
        {
            // ── Legacy flow ──────────────────────────────────────────────────────
            // Stream logs and wait for the container to exit naturally.
            var logStreamTask = StreamContainerLogsAsync(container.ID, onLogLine, cancellationToken);
            var waitResponse = await dockerClient.Containers.WaitContainerAsync(container.ID, cancellationToken);
            await logStreamTask;

            // Explicit cleanup after all logs have been captured. AutoRemove is always disabled to
            // prevent the race where a fast-exiting container is removed before log streaming completes.
            if (!session.KeepContainer)
                await TryStopAndRemoveContainerAsync(container.ID);

            if (waitResponse.StatusCode != 0)
                throw new Exception(
                    $"Agent container exited with code {waitResponse.StatusCode} " +
                    $"(image: {image}, session: {session.Id})");

            return container.ID;
        }

        // For exec flow and HTTP server mode the container must stay alive.
        // Give the entrypoint a moment to complete initial setup (git clone, dockerd, etc.)
        // and then verify the container is still running. If it exited early, capture its
        // logs and throw a meaningful error so the root cause is visible in the session logs.
        await EnsureContainerRunningAsync(container.ID, onLogLine, cancellationToken);

        if (useHttpServerMode)
        {
            // ── HTTP server flow ─────────────────────────────────────────────────
            // The container is running the opencode HTTP server. Drive all session
            // management via the REST API and git operations via docker exec.
            try
            {
                // Step 6: Resolve the host-side port that Docker mapped to the opencode server port.
                var inspect = await dockerClient.Containers.InspectContainerAsync(container.ID, cancellationToken);
                var containerPortKey = $"{OpenCodeHttpApi.DefaultPort}/tcp";
                var ports = inspect.NetworkSettings?.Ports;
                IList<PortBinding>? portBindings = null;
                ports?.TryGetValue(containerPortKey, out portBindings);
                var hostPort = portBindings?.FirstOrDefault()?.HostPort;

                if (string.IsNullOrWhiteSpace(hostPort))
                {
                    var portsInfo = ports is null ? "(null)" : string.Join(", ", ports.Keys);
                    throw new InvalidOperationException(
                        $"Could not determine the host port mapped to container port {OpenCodeHttpApi.DefaultPort} " +
                        $"in container {container.ID[..Math.Min(12, container.ID.Length)]}. " +
                        $"Exposed ports: [{portsInfo}]. " +
                        "Ensure the port binding was configured correctly.");
                }

                var serverBaseUrl = $"http://localhost:{hostPort}";
                await onLogLine($"[DEBUG] HTTP server URL: {serverBaseUrl}", LogStream.Stdout);

                // Emit the server web UI URL as a structured marker so IssueWorker can persist it
                // on the session record for display in the UI.
                await onLogLine($"{ServerWebUiUrlMarker}{serverBaseUrl}", LogStream.Stdout);

                // Step 7: Wait for the opencode server to be ready (up to 60 s).
                await onLogLine("[INFO] Waiting for opencode HTTP server to become ready…", LogStream.Stdout);
                var serverReady = await WaitForHttpServerReadyAsync(serverBaseUrl, maxWaitSeconds: 60, cancellationToken);
                if (!serverReady)
                    throw new InvalidOperationException(
                        $"opencode HTTP server did not become ready within 60 seconds (url: {serverBaseUrl}).");

                await onLogLine("[INFO] opencode HTTP server is ready", LogStream.Stdout);

                // Log server info for diagnostics.
                var serverInfo = await agentHttpApi.GetServerInfoAsync(serverBaseUrl, cancellationToken);
                if (serverInfo is not null)
                    await onLogLine($"[DEBUG] Server info    : {serverInfo[..Math.Min(200, serverInfo.Length)]}", LogStream.Stdout);

                // Log the actual commit SHA checked out by the entrypoint.
                // Also emit the git markers early so IssueWorker captures branch/SHA immediately.
                // EmitGitMarkersAsync runs after the agent completes and will overwrite these with
                // the final committed state, but emitting here ensures the session header is populated
                // even when the agent fails and EmitGitMarkersAsync never runs.
                if (gitRepository is not null)
                {
                    try
                    {
                        var clonedSha = await ExecReadOutputAsync(
                            container.ID, ["git", "rev-parse", "HEAD"], cancellationToken);
                        var clonedBranch = await ExecReadOutputAsync(
                            container.ID, ["git", "branch", "--show-current"], cancellationToken);
                        if (!string.IsNullOrWhiteSpace(clonedSha))
                        {
                            var branchPart = !string.IsNullOrWhiteSpace(clonedBranch)
                                ? $", branch: {clonedBranch}"
                                : string.Empty;
                            await onLogLine($"[INFO] Workspace cloned: SHA={clonedSha}{branchPart}", LogStream.Stdout);
                            await onLogLine($"{GitCommitShaMarker}{clonedSha}", LogStream.Stdout);
                            if (!string.IsNullOrWhiteSpace(clonedBranch))
                                await onLogLine($"{GitBranchMarker}{clonedBranch}", LogStream.Stdout);
                        }
                    }
                    catch (Exception ex)
                    {
                        await onLogLine($"[WARN] Could not read cloned SHA: {ex.Message}", LogStream.Stderr);
                    }
                }

                // Step 8: Create a session and send the task via the HTTP API.
                var httpSessionId = await agentHttpApi.CreateSessionAsync(serverBaseUrl, cancellationToken);
                await onLogLine($"[INFO] Created opencode HTTP session: {httpSessionId}", LogStream.Stdout);

                await agentHttpApi.SendMessageAsync(serverBaseUrl, httpSessionId, taskPrompt, cancellationToken);
                await onLogLine("[INFO] Task sent to opencode HTTP session, waiting for completion…", LogStream.Stdout);

                // Step 9: Poll until the session completes.
                var sessionStatus = await agentHttpApi.WaitForCompletionAsync(
                    serverBaseUrl, httpSessionId,
                    line => onLogLine(line, LogStream.Stdout),
                    cancellationToken);

                await onLogLine($"[INFO] opencode HTTP session completed with status: {sessionStatus}", LogStream.Stdout);

                // Emit the opencode HTTP session ID as a structured marker so IssueWorker can
                // persist it on the session record. This allows subsequent runs for the same issue
                // to continue from this session (by restoring the opencode DB snapshot).
                await onLogLine($"{OpenCodeSessionIdMarker}{httpSessionId}", LogStream.Stdout);

                // Step 10: Check git state and emit markers so IssueWorker can trigger CI/CD.
                if (gitRepository is not null)
                {
                    try
                    {
                        await CheckAndEmitUncommittedChangesAsync(container.ID, onLogLine, cancellationToken);
                        await EmitGitMarkersAsync(container.ID, gitRepository, onLogLine, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        await onLogLine($"[WARN] Git state check failed: {ex.Message}", LogStream.Stderr);
                    }
                }

                if (sessionStatus == AgentHttpSessionStatus.Error)
                    throw new Exception(
                        $"opencode HTTP session ended with error (session: {httpSessionId}, container: {container.ID[..Math.Min(12, container.ID.Length)]})");

                // Return the container ID — the server is still running for potential parallel sessions.
                return container.ID;
            }
            catch
            {
                if (!session.KeepContainer)
                    await TryStopAndRemoveContainerAsync(container.ID);
                throw;
            }
        }

        // ── Exec flow ────────────────────────────────────────────────────────
        // The container is alive (running `sleep infinity`). Drive all agent work via exec.
        try
        {
            // Step 6: Log the actual commit SHA that was checked out by the entrypoint clone.
            // This runs inside the container so we get the real HEAD, not the trigger value.
            // Also emit the git markers early so IssueWorker captures branch/SHA immediately.
            // EmitGitMarkersAsync runs after the agent completes and will overwrite these with
            // the final committed state, but emitting here ensures the session header is populated
            // even when the agent fails and EmitGitMarkersAsync never runs.
            if (gitRepository is not null)
            {
                try
                {
                    var clonedSha = await ExecReadOutputAsync(
                        container.ID, ["git", "rev-parse", "HEAD"], cancellationToken);
                    var clonedBranch = await ExecReadOutputAsync(
                        container.ID, ["git", "branch", "--show-current"], cancellationToken);
                    if (!string.IsNullOrWhiteSpace(clonedSha))
                    {
                        var branchPart = !string.IsNullOrWhiteSpace(clonedBranch)
                            ? $", branch: {clonedBranch}"
                            : string.Empty;
                        await onLogLine($"[INFO] Workspace cloned: SHA={clonedSha}{branchPart}", LogStream.Stdout);
                        await onLogLine($"{GitCommitShaMarker}{clonedSha}", LogStream.Stdout);
                        if (!string.IsNullOrWhiteSpace(clonedBranch))
                            await onLogLine($"{GitBranchMarker}{clonedBranch}", LogStream.Stdout);
                    }
                }
                catch (Exception ex)
                {
                    await onLogLine($"[WARN] Could not read cloned SHA: {ex.Message}", LogStream.Stderr);
                }
            }

            // Step 7: Execute the agent tool via docker exec.
            var agentExitCode = await ExecCommandAsync(container.ID, runnerArgs, onLogLine, cancellationToken);

            // Step 8: Capture the opencode session ID for --fork on subsequent fix runs.
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

            // Step 9: Check git state and emit markers so IssueWorker can trigger CI/CD.
            if (gitRepository is not null)
            {
                try
                {
                    await CheckAndEmitUncommittedChangesAsync(container.ID, onLogLine, cancellationToken);
                    await EmitGitMarkersAsync(container.ID, gitRepository, onLogLine, cancellationToken);
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
        GitRepository? gitRepository,
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

        // After the fix run, check for uncommitted changes and ask opencode to handle them
        // (commit tracked files or update .gitignore for build artifacts) before emitting markers.
        try
        {
            var statusOutput = await ExecReadOutputAsync(
                containerId, ["git", "status", "--porcelain"], cancellationToken);

            if (!string.IsNullOrWhiteSpace(statusOutput))
            {
                await onLogLine("[INFO] Uncommitted changes detected after fix run — re-running opencode to commit them…", LogStream.Stdout);

                var currentBranch = (await ExecReadOutputAsync(
                    containerId, ["git", "branch", "--show-current"], cancellationToken)).Trim();

                var uncommittedIssue = new Issue
                {
                    Id = fixIssue.Id,
                    ProjectId = fixIssue.ProjectId,
                    Number = fixIssue.Number,
                    Title = $"Commit remaining changes for: {fixIssue.Title}",
                    Body =
                        "There are still uncommitted changes after the previous fix run.\n" +
                        "Please commit all changes that should be tracked and add build artifacts or\n" +
                        "generated files to .gitignore so they are not committed.\n" +
                        "Run `git status` to see what is uncommitted.\n" +
                        "IMPORTANT: Do NOT run `git push` — you do not have remote write access.\n" +
                        "Only commit changes locally.",
                    GitBranch = currentBranch,
                };

                var uncommittedArgs = RunnerCommandBuilder.BuildArgsList(agent, uncommittedIssue, forkSessionId: openCodeSessionId);
                if (uncommittedArgs.Count > 0)
                {
                    var exitCode2 = await ExecCommandAsync(containerId, uncommittedArgs, onFixLogLine, cancellationToken);
                    if (exitCode2 != 0)
                        await onLogLine($"[WARN] Uncommitted-changes fix agent exited with code {exitCode2}", LogStream.Stderr);
                }
                else
                {
                    await onLogLine("[WARN] No runner args available for uncommitted-changes fix (RunnerType not set?)", LogStream.Stderr);
                }
            }
        }
        catch (Exception ex)
        {
            await onLogLine($"[WARN] Uncommitted changes check failed: {ex.Message}", LogStream.Stderr);
        }

        // Emit git markers so the caller can capture the updated commit SHA and branch.
        try { await EmitGitMarkersAsync(containerId, gitRepository, onFixLogLine, cancellationToken); }
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
    // HTTP server helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the container is still running after startup.
    /// Polls the container status for up to <c>ContainerStartupPollSeconds</c> seconds.
    /// If the container has exited, captures its recent logs and throws an
    /// <see cref="InvalidOperationException"/> with the exit code and log snippet so the
    /// root cause is visible in the agent session logs.
    /// </summary>
    private async Task EnsureContainerRunningAsync(
        string containerId,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        // Poll until the container is confirmed running or the deadline is reached.
        // The entrypoint can take several seconds (git clone, dockerd startup) before
        // it reaches `exec "$@"` and the container settles into its long-running state.
        const int pollIntervalMs = 500;
        const int maxPollSeconds = 5;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(maxPollSeconds);

        ContainerInspectResponse? inspect = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                inspect = await dockerClient.Containers.InspectContainerAsync(containerId, cancellationToken);
            }
            catch
            {
                // InspectContainerAsync can fail transiently; treat as not-yet-running.
                inspect = null;
            }

            if (inspect?.State?.Running == true)
                return; // container is alive — proceed

            if (inspect?.State?.Running == false)
                break; // container exited — stop polling

            await Task.Delay(pollIntervalMs, cancellationToken);
        }

        // Container is not (or no longer) running. Collect recent logs for diagnostics.
        var exitCode = inspect?.State?.ExitCode ?? -1;
        var recentLogs = await TryGetContainerLogsSnippetAsync(containerId);
        var logSection = string.IsNullOrWhiteSpace(recentLogs)
            ? string.Empty
            : $"\nRecent container output:\n{recentLogs}";

        await onLogLine(
            $"[ERROR] Container {containerId[..Math.Min(12, containerId.Length)]} exited with code {exitCode} during startup. " +
            $"Check the entrypoint logs for errors (git clone failure, dockerd timeout, etc.).{logSection}",
            LogStream.Stderr);

        throw new InvalidOperationException(
            $"Container {containerId[..Math.Min(12, containerId.Length)]} exited with code {exitCode} during startup. " +
            $"Check the session logs for entrypoint output.");
    }

    /// <summary>
    /// Attempts to read the last ~50 lines of a container's stdout/stderr logs.
    /// Returns an empty string on any failure so callers can use it purely for diagnostics.
    /// </summary>
    private async Task<string> TryGetContainerLogsSnippetAsync(string containerId)
    {
        try
        {
            var lines = new System.Collections.Generic.List<string>();
            await StreamContainerLogsAsync(
                containerId,
                (line, _) => { lines.Add(line); return Task.CompletedTask; },
                CancellationToken.None);
            // Return only the last 50 lines to keep the error message concise.
            var tail = lines.Count > 50 ? lines.Skip(lines.Count - 50) : lines;
            return string.Join('\n', tail);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Polls the agent's HTTP server until it is ready to accept requests or the
    /// <paramref name="maxWaitSeconds"/> deadline is reached. Returns <c>true</c> when
    /// the server becomes ready, <c>false</c> on timeout.
    /// </summary>
    private async Task<bool> WaitForHttpServerReadyAsync(
        string serverBaseUrl,
        int maxWaitSeconds,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(maxWaitSeconds);
        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (await agentHttpApi.IsReadyAsync(serverBaseUrl, cancellationToken))
                return true;
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
        return false;
    }

    /// <summary>
    /// Reads the <c>entrypoint.sh</c> embedded resource compiled into this assembly and
    /// copies it into a (not-yet-started) container at <c>/usr/local/bin/entrypoint.sh</c>
    /// with executable permissions (0755).
    ///
    /// This keeps the container entrypoint in sync with the execution client binary so that
    /// updates to the script (new env var handling, tool setup, etc.) take effect without
    /// requiring a new Docker image build.
    /// </summary>
    private async Task InjectEntrypointAsync(string containerId, CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        // The resource name is the <LogicalName> from the .csproj, which overrides the default
        // namespace-qualified name. The .fsi/reflection test confirms it resolves to "entrypoint.sh".
        var resourceName = "entrypoint.sh";
        await using var resourceStream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found in assembly '{assembly.GetName().Name}'. " +
                "Ensure the file is included as an EmbeddedResource in the project.");

        // Normalise to Unix LF line endings so the shebang (#!/usr/bin/env bash) is interpreted
        // correctly on Linux containers. The embedded resource may contain CRLF if the build ran
        // on Windows or if git.autocrlf converted the line endings at checkout time.
        // Without this the first line becomes "#!/usr/bin/env bash\r" and the kernel reports
        // "/usr/bin/env: 'bash\r': No such file or directory", killing the container on startup.
        var rawContent = await new System.IO.StreamReader(resourceStream, leaveOpen: false).ReadToEndAsync(cancellationToken);
        var normalised = rawContent.Replace("\r\n", "\n").Replace("\r", "\n");
        var normalisedBytes = System.Text.Encoding.UTF8.GetBytes(normalised);
        var normalisedStream = new MemoryStream(normalisedBytes);

        // Build a tar archive containing entrypoint.sh at the root (target path: /usr/local/bin/).
        using var tarBuffer = new MemoryStream();
        await using (var tarWriter = new TarWriter(tarBuffer, TarEntryFormat.Ustar, leaveOpen: true))
        {
            var entry = new UstarTarEntry(TarEntryType.RegularFile, "entrypoint.sh")
            {
                // Octal 0755: owner rwx, group r-x, other r-x
                Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                       | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                       | UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
                DataStream = normalisedStream,
            };
            await tarWriter.WriteEntryAsync(entry, cancellationToken);
        }

        tarBuffer.Seek(0, SeekOrigin.Begin);

        await dockerClient.Containers.ExtractArchiveToContainerAsync(
            containerId,
            new CopyToContainerParameters { Path = "/usr/local/bin" },
            tarBuffer,
            cancellationToken);
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
    /// Executes <paramref name="cmd"/> inside a running container and returns the stdout output
    /// as a trimmed string. Stderr is intentionally excluded — git and other tools emit error
    /// messages on stderr that would otherwise pollute captured values (e.g. SHA, branch name).
    /// Output is not forwarded to any log sink.
    /// </summary>
    private async Task<string> ExecReadOutputAsync(
        string containerId,
        IReadOnlyList<string> cmd,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        await ExecCommandAsync(containerId, cmd,
            (line, stream) =>
            {
                if (stream == LogStream.Stdout) sb.AppendLine(line);
                return Task.CompletedTask;
            },
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
                // Session list format: <id>  <title>  <updated> — first whitespace-delimited token is the ID.
                // opencode lists sessions newest-first, so the first real session ID line is the most recent.
                // Skip the header line ("Session ID  Title  Updated") and separator lines by requiring the
                // token to start with the "ses_" prefix used by all opencode session identifiers.
                if (lastSessionId is null)
                {
                    var trimmedLine = line.TrimStart();
                    var tokenEnd = trimmedLine.IndexOfAny([' ', '\t']);
                    var token = (tokenEnd > 0 ? trimmedLine[..tokenEnd] : trimmedLine).Trim();
                    if (token.StartsWith("ses_", StringComparison.Ordinal))
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
    /// Returns <c>true</c> when the push succeeded, <c>false</c> otherwise.
    ///
    /// The IssuePit execution client always pushes Working-mode repos so that CI/CD can
    /// load the branch. Push restrictions for agent tools (opencode, CLI commands) are
    /// enforced separately via the in-container git wrapper and the opencode plugin,
    /// controlled by the <c>ISSUEPIT_PUSH_POLICY</c> environment variable.
    ///
    /// Safety guard: the execution client never pushes to the repository's configured
    /// default branch (main/master) to prevent accidental overwriting of the main line.
    /// </summary>
    private async Task<bool> EmitGitMarkersAsync(
        string containerId,
        GitRepository? gitRepository,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var branch = await ExecReadOutputAsync(
            containerId, ["git", "branch", "--show-current"], cancellationToken);
        var commitSha = await ExecReadOutputAsync(
            containerId, ["git", "rev-parse", "HEAD"], cancellationToken);

        var pushSucceeded = false;

        // Push is allowed to fail — credentials may not be configured yet.
        if (!string.IsNullOrWhiteSpace(branch))
        {
            // Safety guard: never let the execution client push to the repository's default branch.
            var defaultBranch = gitRepository?.DefaultBranch?.Trim();
            var branchTrimmed = branch.Trim();
            var isDefaultBranch = branchTrimmed.Equals(defaultBranch, StringComparison.OrdinalIgnoreCase)
                || branchTrimmed.Equals("main", StringComparison.OrdinalIgnoreCase)
                || branchTrimmed.Equals("master", StringComparison.OrdinalIgnoreCase);

            if (isDefaultBranch)
            {
                await onLogLine(
                    $"[entrypoint] Push to default branch '{branchTrimmed}' is not allowed — push skipped",
                    LogStream.Stdout);
            }
            else
            {
                await onLogLine($"[entrypoint] Pushing branch '{branch}' to origin…", LogStream.Stdout);
                // Use the real git binary path written by the entrypoint before it installed the
                // push-blocking wrapper at /usr/local/bin/git. This allows the execution client to
                // push branches via docker exec without hitting the wrapper meant to stop agents.
                var realGit = (await ExecReadOutputAsync(
                    containerId,
                    ["/bin/sh", "-c", "cat /tmp/.issuepit-real-git 2>/dev/null || echo /usr/bin/git"],
                    cancellationToken)).Trim();
                var pushExit = await ExecCommandAsync(containerId, [realGit, "push", "origin", branch],
                    async (line, stream) => await onLogLine($"[entrypoint] {line}", stream),
                    cancellationToken);
                if (pushExit != 0)
                {
                    await onLogLine(
                        "[entrypoint] Push failed (allowed — credentials may not be configured or push was rejected)",
                        LogStream.Stdout);
                    // Emit a structured marker so IssueWorker can trigger a .git archive upload for recovery.
                    await onLogLine(GitPushFailedMarker, LogStream.Stdout);
                }
                else
                    pushSucceeded = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(commitSha))
            await onLogLine($"{GitCommitShaMarker}{commitSha}", LogStream.Stdout);
        if (!string.IsNullOrWhiteSpace(branch))
            await onLogLine($"{GitBranchMarker}{branch}", LogStream.Stdout);

        return pushSucceeded;
    }

    /// <summary>
    /// Extracts the <c>/workspace/.git</c> directory from the running container using the
    /// Docker archive API and returns the raw tar stream. The caller is responsible for
    /// disposing the returned stream. Returns <c>null</c> if the archive could not be read.
    /// </summary>
    internal async Task<Stream?> TryGetGitArchiveStreamAsync(
        string containerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await dockerClient.Containers.GetArchiveFromContainerAsync(
                containerId,
                new Docker.DotNet.Models.ContainerPathStatParameters { Path = "/workspace/.git" },
                false,
                cancellationToken);
            return response.Stream;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not extract .git archive from container {ContainerId}", containerId);
            return null;
        }
    }

    /// <summary>
    /// Extracts the opencode SQLite database from the container at
    /// <c>/root/.local/share/opencode/opencode.db</c> and returns the raw tar stream.
    /// The tar archive preserves the relative path so it can be re-injected into a new container
    /// at path <c>/</c> to restore to the same location.
    /// The caller is responsible for disposing the returned stream.
    /// Returns <c>null</c> if the file does not exist or cannot be read (e.g. no sessions were created).
    /// </summary>
    internal async Task<Stream?> TryGetOpenCodeDbStreamAsync(
        string containerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await dockerClient.Containers.GetArchiveFromContainerAsync(
                containerId,
                new Docker.DotNet.Models.ContainerPathStatParameters { Path = "/root/.local/share/opencode/opencode.db" },
                false,
                cancellationToken);
            return response.Stream;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not extract opencode DB from container {ContainerId} (may not exist yet)", containerId);
            return null;
        }
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

    /// <summary>
    /// Replaces <c>localhost</c> and <c>127.0.0.1</c> host references in a URL with
    /// <c>host.docker.internal</c> so agent containers can reach services running on the
    /// Docker host. Handles both port-qualified (<c>http://localhost:8080/</c>) and
    /// default-port (<c>http://localhost/path</c>) forms.
    /// Returns <c>null</c> when the input is null or empty.
    /// </summary>
    internal static string? ToDockerHostUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;
        if (!uri.IsLoopback) return url; // already points at a non-localhost host — leave it alone

        var builder = new UriBuilder(uri) { Host = "host.docker.internal" };
        return builder.Uri.ToString().TrimEnd('/');
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
