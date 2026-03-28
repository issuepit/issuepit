using System.Collections.Concurrent;
using System.Formats.Tar;
using System.Reflection;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.Core.Runners;
using IssuePit.DockerRuntime;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>
/// Runs the agent inside a local Docker container with Docker-in-Docker (DinD) support.
///
/// For exec flow and HTTP server mode the container is started with
/// <c>Entrypoint=["/bin/sh","-c"]</c> and <c>Cmd=["tail -f /dev/null"]</c>, bypassing any
/// <c>ENTRYPOINT</c> or <c>CMD</c> baked into the Docker image. All workspace setup —
/// git clone, git identity, push wrapper, tool restore, DinD, DNS proxy, opencode config —
/// is then driven from C# via <c>docker exec</c> so every log line flows through
/// <c>onLogLine</c> → UI and failures produce clear, actionable error messages.
///
/// Previously this setup ran inside <c>entrypoint.sh</c> as PID 1 before exec was possible,
/// causing silent failures (git clone errors visible only in <c>docker logs</c>, not the UI)
/// and "container is not running" conflicts when the entrypoint exited early.
///
/// When <see cref="Agent.RunnerType"/> is null (legacy mode), the container runs its
/// default CMD and is waited on as before (entrypoint.sh is still injected).
/// </summary>
// CS9107: dockerClient is used directly in this class for DI convenience and also passed to
// DockerRuntimeBase which stores it as the protected DockerClient property.
// Both references point to the same object — there is no semantic duplication.
#pragma warning disable CS9107
public class DockerAgentRuntime(
    ILogger<DockerAgentRuntime> logger,
    DockerClient dockerClient,
    IConfiguration configuration,
    IAgentHttpApi agentHttpApi,
    IHttpClientFactory httpClientFactory)
    : DockerRuntimeBase(logger, dockerClient), IExecCapableRuntime
#pragma warning restore CS9107
{
    // Docker image used to run agents. Uses the IssuePit helper-opencode-act image which includes
    // the opencode CLI, Docker Engine (DinD), act, .NET SDK, Node.js, and Playwright.
    // Overridden by agent.DockerImage when set.
    internal const string DefaultDockerImage = "ghcr.io/issuepit/issuepit-helper-opencode-act:main-dotnet10-node24";

    // Default act runner image injected into /root/.config/act/actrc so that when the agent
    // invokes `act` to run CI workflows inside the container, the inner workflow job containers
    // use an image that includes .NET 10, Node.js, and Playwright — matching the outer helper image.
    // Can be overridden via the "Agent__ActRunnerImage" configuration key.
    private const string DefaultActRunnerImage = "ghcr.io/issuepit/issuepit-act-runner:latest";

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
    /// Emitted just before post-agent operations (session capture, uncommitted-changes check, git push)
    /// so IssueWorker can tag subsequent log lines with the <c>PostRun</c> section.
    /// </summary>
    internal const string PostRunStartMarker = "[ISSUEPIT:POST_RUN_START]";
    /// <summary>
    /// Emitted when the agent is running in HTTP server mode; carries the URL of the agent's web UI.
    /// IssueWorker captures this and persists it on <see cref="AgentSession.ServerWebUiUrl"/>.
    /// </summary>
    internal const string ServerWebUiUrlMarker = "[ISSUEPIT:SERVER_WEB_UI_URL]=";
    /// <summary>
    /// Emitted when the agent is in manual mode. Carries the full Docker container ID so
    /// IssueWorker can persist it on <see cref="AgentSession.ContainerId"/>, enabling the
    /// API terminal endpoint to attach to the container for live terminal I/O.
    /// </summary>
    internal const string ManualModeContainerIdMarker = "[ISSUEPIT:MANUAL_MODE_CONTAINER_ID]=";

    private static string AppVersion =>
        Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

    // In-process pull cache: tracks the last pull time and image digest per image reference.
    // Used to skip the registry round-trip when the image was pulled recently (within PullCacheSeconds).
    // The cache is process-scoped (static) so it survives across multiple agent runs on the same instance.
    private static readonly ConcurrentDictionary<string, (DateTime PullTime, string? Digest)> ImagePullCache = new();

    /// <summary>Default number of seconds to cache a pull result (30 minutes).</summary>
    private const int DefaultPullCacheSeconds = 1800;

    /// <summary>Number of characters of the digest prefix to show in log lines (enough to identify sha256:abcdef…).</summary>
    private const int DigestDisplayLength = 19;

    /// <summary>
    /// Number of seconds to cache a successful pull result before re-checking the registry.
    /// Configured via <c>Execution:ImagePullCacheSeconds</c>. Defaults to <see cref="DefaultPullCacheSeconds"/> (30 minutes).
    /// Set to 0 to always pull from the registry.
    /// </summary>
    private int ImagePullCacheSeconds =>
        int.TryParse(configuration["Execution:ImagePullCacheSeconds"], out var v) ? v : DefaultPullCacheSeconds;

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
        GitRepository? cloneRepository,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        // Resolve effective clone source. When only one remote is configured both are the same.
        // When multiple remotes: gitRepository = Working-mode push target,
        //                        cloneRepository = freshest remote (highest LastFetchedAt).
        var cloneRepo = cloneRepository ?? gitRepository;

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
        if (agent.ManualMode)
            await onLogLine($"[DEBUG] Agent mode     : Manual (live terminal session)", LogStream.Stdout);
        await onLogLine($"[DEBUG] DinD           : isolated (Privileged=true, in-container dockerd)", LogStream.Stdout);
        if (agent.DisableInternet)
            await onLogLine($"[DEBUG] Internet       : restricted", LogStream.Stdout);
        if (session.KeepContainer)
            await onLogLine($"[DEBUG] Keep container : true (container will not be removed on exit)", LogStream.Stdout);
        if (cloneRepo is not null)
        {
            await onLogLine($"[DEBUG] Git clone src  : {cloneRepo.RemoteUrl} ({cloneRepo.Mode})", LogStream.Stdout);
            if (gitRepository is not null && gitRepository != cloneRepo)
                await onLogLine($"[DEBUG] Git push target: {gitRepository.RemoteUrl} ({gitRepository.Mode})", LogStream.Stdout);
            // Determine the branch the container will check out: issue.GitBranch takes precedence
            // (feature branch for this issue), otherwise falls back to the clone source's default branch.
            var effectiveBranch = !string.IsNullOrWhiteSpace(issue.GitBranch)
                ? issue.GitBranch
                : cloneRepo.DefaultBranch;
            if (!string.IsNullOrWhiteSpace(effectiveBranch))
                await onLogLine($"[DEBUG] Git branch     : {effectiveBranch}", LogStream.Stdout);

            // Validate that we have a branch to clone. When no feature branch is set on the issue
            // the entrypoint uses DefaultBranch as the base — if that is also empty there is
            // nothing to clone. Fail here with a clear message rather than letting the container
            // start and exit with a cryptic git error.
            if (string.IsNullOrWhiteSpace(issue.GitBranch) && string.IsNullOrWhiteSpace(cloneRepo.DefaultBranch))
                throw new InvalidOperationException(
                    $"Clone-source remote '{cloneRepo.RemoteUrl}' has no DefaultBranch configured and the issue has no GitBranch set. " +
                    "Set the default branch on at least one remote in the project's git repository settings before running an agent.");
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

        // Step 1: Pull the container image, logging the digest so we can verify which version is running.
        // Smart pull: skip the registry round-trip if the image was pulled recently (within ImagePullCacheSeconds).
        // If the cache entry is stale or absent, pull from the registry and update the cache.
        var pullCacheSeconds = ImagePullCacheSeconds;
        var cached = ImagePullCache.TryGetValue(image, out var cacheEntry);
        var cacheAgeSec = cached ? (DateTime.UtcNow - cacheEntry.PullTime).TotalSeconds : double.MaxValue;
        var skipPull = cached && pullCacheSeconds > 0 && cacheAgeSec < pullCacheSeconds;

        await onLogLine($"[DEBUG] Pulling image  : {image}", LogStream.Stdout);
        if (skipPull)
        {
            var cacheAgeMin = (int)(cacheAgeSec / 60.0);
            await onLogLine($"[DEBUG] Pull skipped   : image pulled {cacheAgeMin}m ago (cache: {pullCacheSeconds}s), using cached version", LogStream.Stdout);
            if (!string.IsNullOrEmpty(cacheEntry.Digest))
                await onLogLine($"[DEBUG] Image digest   : {cacheEntry.Digest}", LogStream.Stdout);
        }
        else
        {
            var pullStart = DateTime.UtcNow;
            await onLogLine($"[DEBUG] Pull started   : {pullStart:u}", LogStream.Stdout);
            await PullImageAsync(image, cancellationToken);
            var pullDuration = (DateTime.UtcNow - pullStart).TotalSeconds;
            await onLogLine($"[DEBUG] Pull finished  : {DateTime.UtcNow:u} (took {pullDuration:F1}s)", LogStream.Stdout);

            // Inspect image to get the digest so we can verify which version is running
            // and detect when a pull updates the image.
            var digest = await TryGetImageDigestAsync(image, cancellationToken);
            if (!string.IsNullOrEmpty(digest))
                await onLogLine($"[DEBUG] Image digest   : {digest}", LogStream.Stdout);

            // Check whether the image was actually updated (digest changed).
            if (cached && !string.IsNullOrEmpty(cacheEntry.Digest) && !string.IsNullOrEmpty(digest))
            {
                if (cacheEntry.Digest != digest)
                    await onLogLine($"[DEBUG] Image updated  : {cacheEntry.Digest[..Math.Min(DigestDisplayLength, cacheEntry.Digest.Length)]} → {digest[..Math.Min(DigestDisplayLength, digest.Length)]}", LogStream.Stdout);
                else
                    await onLogLine($"[DEBUG] Image up-to-date: digest unchanged", LogStream.Stdout);
            }

            ImagePullCache[image] = (DateTime.UtcNow, digest);
        }

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
        var issuePitApiUrl = ToDockerHostUrl(configuration["ApiServer:BaseUrl"]);
        var env = AgentEnvironmentBuilder.Build(session, agent, issue, credentials, gitRepository,
            cloneRepository: cloneRepo != gitRepository ? cloneRepo : null,
            issuePitMcpUrl: issuePitMcpUrl,
            issuePitApiUrl: issuePitApiUrl);
        if (!string.IsNullOrWhiteSpace(issuePitMcpUrl))
            await onLogLine($"[DEBUG] IssuePit MCP   : {issuePitMcpUrl}", LogStream.Stdout);
        if (!string.IsNullOrWhiteSpace(issuePitApiUrl))
            await onLogLine($"[DEBUG] IssuePit API   : {issuePitApiUrl}", LogStream.Stdout);

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
        // Manual mode      — container CMD = "sleep infinity"; C# drives all workspace setup via
        //                    docker exec (git clone, credentials, opencode config) but does NOT
        //                    run the runner CLI. A live terminal session is opened in the web UI
        //                    so the user can type commands interactively. The container ID is emitted
        //                    as a [ISSUEPIT:MANUAL_MODE_CONTAINER_ID]= marker for IssueWorker.
        // Exec flow        — container CMD = "tail -f /dev/null"; C# drives all agent commands via
        //                    docker exec. Keeps the same opencode session files across fix runs.
        var useHttpServerMode = agent.UseHttpServer && agent.RunnerType == RunnerType.OpenCode;
        var useManualMode = agent.ManualMode;

        var comments = issue.Comments.Count > 0 ? (IReadOnlyList<IssueComment>)issue.Comments : null;
        if (comments is not null)
            await onLogLine($"[DEBUG] Comments       : {comments.Count} comment(s) included in prompt", LogStream.Stdout);

        // Build CLI args for exec flow (not used in HTTP server mode or manual mode).
        // Only pass --session <id> when a DB snapshot was actually loaded; without the restored
        // DB the session does not exist in the fresh container and opencode would throw NotFoundError.
        // File paths (--file) are not included here — they are appended after attachments are
        // downloaded to the container in Step D.5.
        var runnerArgs = (useHttpServerMode || useManualMode) ? [] : RunnerCommandBuilder.BuildArgsList(
            agent, issue,
            continueSessionId: session.PreviousOpenCodeDbTar is { Length: > 0 } ? session.PreviousOpenCodeSessionId : null,
            comments: comments);
        // Exec flow is the only path for autonomous agent runs (non-HTTP-server, non-manual).
        // Previously there was a "legacy flow" (agent.RunnerType == null) that ran the container's
        // default CMD and waited for it to exit — that path has been removed. All agent runs now
        // use docker exec for workspace setup and agent execution, keeping the container alive
        // and surfacing every log line through onLogLine → UI.
        var useExecFlow = !useHttpServerMode && !useManualMode;

        // Tracks container paths of downloaded issue attachments; populated in Step D.5 and
        // used when rebuilding runnerArgs (including in fallback cases).
        var downloadedAttachmentPaths = new List<string>();

        if (runnerArgs.Count > 0)
        {
            // Log the CLI command (e.g. "opencode run --model ...") without the task text.
            var cmdDisplay = string.Join(" ", runnerArgs.Take(runnerArgs.Count - 1));
            await onLogLine($"[DEBUG] Runner cmd     : {cmdDisplay}", LogStream.Stdout);
        }
        else if (session.CustomCmd is { Length: > 0 })
        {
            await onLogLine($"[DEBUG] Runner cmd     : {string.Join(" ", session.CustomCmd)}", LogStream.Stdout);
        }

        // Build the task prompt (used for the exec flow and HTTP server mode).
        // In manual mode the user drives the agent interactively, so skip building/logging it.
        var taskPrompt = useManualMode ? string.Empty : RunnerCommandBuilder.BuildTaskPrompt(issue, comments);

        // Log the task prompt that will be passed to the agent so it is always visible in the logs.
        if (!useManualMode)
        {
            await onLogLine($"[DEBUG] Task prompt    :", LogStream.Stdout);
            foreach (var promptLine in taskPrompt.Split('\n'))
                await onLogLine($"[DEBUG]   {promptLine}", LogStream.Stdout);
        }

        // Step 4: Configure DNS-based firewall when internet access should be restricted.
        var dns = agent.DisableInternet ? GetRestrictedDns() : null;

        logger.LogInformation(
            "Creating Docker container from image {Image} for agent {AgentId} (DisableInternet={DisableInternet}, ExecFlow={UseExecFlow}, HttpServer={UseHttpServer}, Manual={UseManualMode})",
            image, agent.Id, agent.DisableInternet, useExecFlow, useHttpServerMode, useManualMode);

        var hostConfig = new HostConfig
        {
            Privileged = true,
            // Never auto-remove: the container's lifetime is managed explicitly.
            // AutoRemove races with log capture — the container can be removed before logs are fully
            // streamed, producing NotFound errors. We always remove the container after all work is done.
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

        // Container CMD and Entrypoint:
        // All agent runs (exec flow, HTTP server mode, manual mode) bypass the image's ENTRYPOINT
        // by overriding Entrypoint=["/bin/sh","-c"] and Cmd=["tail -f /dev/null"]. This keeps the
        // container alive immediately without running any baked-in entrypoint. All workspace setup
        // (git clone, DinD, DNS, opencode config) and agent execution are driven via docker exec
        // from C# so every log line flows through onLogLine → UI.
        IList<string> containerCmd = ["tail -f /dev/null"];

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
            // Override the image's Entrypoint so it does NOT run as PID 1. The container starts
            // immediately with "tail -f /dev/null" and is kept alive for docker exec. All setup is
            // done in C#.
            Entrypoint = ["/bin/sh", "-c"],
            Labels = new Dictionary<string, string>
            {
                ["issuepit.session-id"] = session.Id.ToString(),
                ["issuepit.issue-id"] = issue.Id.ToString(),
                ["issuepit.agent-id"] = agent.Id.ToString(),
            },
        };

        // Step 5: Create the container.
        var container = await dockerClient.Containers.CreateContainerAsync(
            createParams, cancellationToken);

        await onLogLine("[DEBUG] Entrypoint     : bypassed — workspace setup via docker exec", LogStream.Stdout);

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
                // Docker's GetArchiveFromContainerAsync for a FILE path returns a tar with just the
                // basename (e.g. "opencode.db"), not the full relative path. Extracting at "/" would
                // place the file at "/opencode.db" instead of the correct location. We must extract
                // at the parent directory so Docker places it at the right path.
                await dockerClient.Containers.ExtractArchiveToContainerAsync(
                    container.ID,
                    new CopyToContainerParameters { Path = "/root/.local/share/opencode" },
                    dbStream,
                    cancellationToken);
                await onLogLine("[INFO] opencode DB snapshot injected — continuing from previous session", LogStream.Stdout);
            }
            catch (Exception ex)
            {
                await onLogLine($"[WARN] Failed to inject opencode DB snapshot: {ex.Message} — starting fresh session", LogStream.Stderr);
                logger.LogWarning(ex, "Failed to inject opencode DB into container {ContainerId}", container.ID);
                // The session does not exist in the container without the restored DB; rebuild
                // runner args without --session to avoid a "Session not found" crash in opencode.
                session.PreviousOpenCodeSessionId = null;
                runnerArgs = RunnerCommandBuilder.BuildArgsList(agent, issue, comments: comments);
            }
        }

        // Emit previous session ID so it appears in the logs for traceability.
        if (!string.IsNullOrWhiteSpace(session.PreviousOpenCodeSessionId))
            await onLogLine($"[INFO] Continuing from previous opencode session: {session.PreviousOpenCodeSessionId}", LogStream.Stdout);

        // The container stays alive (tail -f /dev/null). Verify it is running, then drive all
        // workspace setup via docker exec so every log line flows through onLogLine → UI.
        await EnsureContainerRunningAsync(container.ID, onLogLine, cancellationToken);

        // ── Workspace setup ────────────────────────────────────────────────────
        // All steps are executed via docker exec so that:
        //   1. Every log line (including git clone progress/errors) is visible in the UI.
        //   2. Failures (wrong credentials, missing branch) throw with clear error messages.
        //   3. The container stays alive throughout setup — no more "container is not running" conflicts.
        await onLogLine("[INFO] Starting workspace setup via docker exec…", LogStream.Stdout);

        // Step A: Clone the workspace (if a git repository is configured).
        if (cloneRepo is not null)
        {
            // Use issue.GitBranch only when it is set AND differs from the base branch —
            // meaning a dedicated feature branch was already created for this issue in a
            // prior run. When issue.GitBranch equals the default branch (or is empty) we
            // generate a fresh feature branch name so we never work directly on main/master.
            var featureBranch = !string.IsNullOrWhiteSpace(issue.GitBranch)
            && !string.Equals(issue.GitBranch, cloneRepo.DefaultBranch, StringComparison.OrdinalIgnoreCase)
            ? issue.GitBranch
            : GenerateFeatureBranchName(issue.Number, issue.Title ?? string.Empty);
            // Full-history clone is required when the push target (Working remote) differs from the
            // clone source (e.g. Release/upstream remote with the most commits). Without it git push
            // fails with "remote: fatal: did not receive expected object" because a shallow clone's
            // parent commit is not present in the working remote's object store.
            var fullHistory = gitRepository is not null && gitRepository.Id != cloneRepo.Id;
            await CloneWorkspaceAsync(
            container.ID,
            cloneRepo.RemoteUrl,
            cloneRepo.DefaultBranch,
            featureBranch,
            cloneRepo.AuthUsername,
            cloneRepo.AuthToken,
            onLogLine,
            cancellationToken,
            fullHistory: fullHistory);

            await SetupGitIdentityAndBranchAsync(container.ID, featureBranch, onLogLine, cancellationToken);
            await InstallGitPushWrapperAsync(container.ID, onLogLine, cancellationToken);
            await SetupWorkspaceToolsAsync(container.ID, onLogLine, cancellationToken);
        }

        // Step B: Start Docker-in-Docker (needed for act and any docker commands by the agent).
        await StartDindAsync(container.ID, onLogLine, cancellationToken);

        // Step B2: Write actrc so that when the agent invokes `act` to run CI workflows,
        // the inner workflow job containers use the issuepit-act-runner image (which has
        // .NET 10, Node.js, Playwright, and the same toolchain as the outer helper image)
        // instead of the default catthehacker/ubuntu image (which only ships .NET 8).
        // Best-effort: failure is non-fatal and only logs a warning — the agent will still
        // start, but CI workflow steps that rely on .NET 10 may fail if act is invoked.
        var actRunnerImage = configuration["Agent__ActRunnerImage"] ?? DefaultActRunnerImage;
        await SetupActrcAsync(container.ID, actRunnerImage, onLogLine, cancellationToken);

        // Step C: Start DNS logging/firewall proxy.
        await SetupDnsProxyAsync(container.ID, agent.DisableInternet, onLogLine, cancellationToken);

        // Step D: Write opencode config (replaces the Python script that ran inside entrypoint.sh).
        if (agent.RunnerType == RunnerType.OpenCode)
        {
            var agentsJson = AgentEnvironmentBuilder.BuildAgentsJson(agent);
            var extraMcpJson = AgentEnvironmentBuilder.BuildExtraMcpJson(agent);
            await WriteOpencodeConfigAsync(
            container.ID,
            mcpUrl: issuePitMcpUrl,
            agentsJson: string.IsNullOrEmpty(agentsJson) ? null : agentsJson,
            extraMcpJson: string.IsNullOrEmpty(extraMcpJson) ? null : extraMcpJson,
            port: useHttpServerMode ? OpenCodeHttpApi.DefaultPort : null,
            password: useHttpServerMode ? agent.HttpServerPassword : null,
            pluginsJson: null,
            onLogLine: onLogLine,
            cancellationToken: cancellationToken);

            // Step D2: Log which agents are configured in opencode for diagnostics.
            await LogOpenCodeAgentsAsync(container.ID, cancellationToken);

            // Step D2.5: Log which MCP servers are configured in opencode for diagnostics.
            await LogOpenCodeMcpServersAsync(container.ID, onLogLine, cancellationToken);

            // Step D.5: Download issue attachments into the container and
            // append --file <path> for each one so the agent has direct access to the files.
            // https://opencode.ai/docs/cli/#run-1
            if (issue.PromptAttachments.Count > 0)
            {
            try
            {
                var paths = await DownloadAttachmentsToContainerAsync(
                container.ID, issue.PromptAttachments, onLogLine, cancellationToken);
                downloadedAttachmentPaths.AddRange(paths);

                if (downloadedAttachmentPaths.Count > 0)
                {
                // Rebuild runnerArgs to include --file flags for each downloaded attachment.
                runnerArgs = RunnerCommandBuilder.BuildArgsList(
                    agent, issue,
                    continueSessionId: session.PreviousOpenCodeDbTar is { Length: > 0 } ? session.PreviousOpenCodeSessionId : null,
                    comments: comments,
                    filePaths: downloadedAttachmentPaths);
                var updatedCmdDisplay = string.Join(" ", runnerArgs.Take(runnerArgs.Count - 1));
                await onLogLine($"[DEBUG] Runner cmd     : {updatedCmdDisplay}", LogStream.Stdout);
                }
            }
            catch (Exception ex)
            {
                await onLogLine($"[WARN] Failed to download issue attachments: {ex.Message} — continuing without attached files", LogStream.Stderr);
                logger.LogWarning(ex, "Failed to download issue attachments into container {ContainerId}", container.ID);
            }
            }
        }

        // Step E (HTTP server mode only): Start opencode as a background process via exec.
        // With the entrypoint bypassed, opencode is no longer the container CMD — we must
        // start it explicitly. Running it as a background process (nohup … &) returns
        // immediately so we can wait for HTTP readiness in the next step.
        if (useHttpServerMode)
        {
            await onLogLine("[INFO] Starting opencode HTTP server via exec…", LogStream.Stdout);
            var startScript = "cd /workspace && nohup opencode > /tmp/opencode-server.log 2>&1 &";
            await ExecCommandAsync(container.ID, ["/bin/sh", "-c", startScript],
            (_, _) => Task.CompletedTask, cancellationToken, workingDir: "/");
        }

        // Step F: Verify the previous opencode session exists in the restored DB.
        // Even when the DB injection succeeded the session may be missing after a schema migration
        // or if the tar was extracted to the wrong path. Fall back to a fresh session in that case
        // to avoid a "Session not found" crash when opencode runs with --session <id>.
        if (!string.IsNullOrEmpty(session.PreviousOpenCodeSessionId) && agent.RunnerType == RunnerType.OpenCode)
        {
            var sessionVerified = await VerifyOpenCodeSessionExistsAsync(
            container.ID, session.PreviousOpenCodeSessionId, onLogLine, cancellationToken);
            if (!sessionVerified)
            {
            await onLogLine(
                $"[WARN] Previous opencode session {session.PreviousOpenCodeSessionId} not found in restored DB — starting fresh session",
                LogStream.Stderr);
            session.PreviousOpenCodeSessionId = null;
            runnerArgs = RunnerCommandBuilder.BuildArgsList(agent, issue, comments: comments,
                filePaths: downloadedAttachmentPaths.Count > 0 ? downloadedAttachmentPaths : null);
            var newCmdDisplay = string.Join(" ", runnerArgs.Take(runnerArgs.Count - 1));
            await onLogLine($"[DEBUG] Runner cmd     : {newCmdDisplay}", LogStream.Stdout);
            }
        }

        // Step G: Inject backed-up auth.json if provided via the ISSUEPIT_AUTH_JSON_CONTENT credential.
        // This allows users to authenticate once in a manual terminal session and reuse those
        // credentials in subsequent autonomous runs.
        var authJsonEntry = env.FirstOrDefault(e => e.StartsWith("ISSUEPIT_AUTH_JSON_CONTENT=", StringComparison.Ordinal));
        if (authJsonEntry is not null)
        {
            var authJsonContent = authJsonEntry["ISSUEPIT_AUTH_JSON_CONTENT=".Length..];
            {
            try
            {
                await InjectAuthJsonAsync(container.ID, authJsonContent, onLogLine, cancellationToken);
            }
            catch (Exception ex)
            {
                await onLogLine($"[WARN] Failed to inject auth.json: {ex.Message} — proceeding without auth restore", LogStream.Stderr);
                logger.LogWarning(ex, "Failed to inject auth.json into container {ContainerId}", container.ID);
            }
            }
        }

        await onLogLine("[INFO] Workspace setup complete", LogStream.Stdout);

        if (useManualMode)
        {
            // ── Manual mode ───────────────────────────────────────────────────────
            // Workspace is fully set up (git clone, credentials, opencode config, DinD).
            // The container stays alive for the user to interact with via a live terminal.
            // Emit the container ID as a marker so IssueWorker can persist it on the session.
            try
            {
                // Inject the CI/CD trigger script so the user can push and trigger a CI/CD run
                // with a single command from the terminal.
                await InjectCiCdTriggerScriptAsync(container.ID, onLogLine, cancellationToken);
            }
            catch (Exception ex)
            {
                await onLogLine($"[WARN] Failed to inject CI/CD trigger script: {ex.Message}", LogStream.Stderr);
                logger.LogWarning(ex, "Failed to inject CI/CD trigger script in container {ContainerId}", container.ID);
            }

            // Emit the container ID marker so IssueWorker can persist it on AgentSession.ContainerId.
            await onLogLine($"{ManualModeContainerIdMarker}{container.ID}", LogStream.Stdout);
            await onLogLine("[INFO] Manual mode: container ready — connect via the terminal in the web UI", LogStream.Stdout);

            // Return the container ID. The container stays running; IssueWorker will keep the session
            // in Running state until the user explicitly cancels it.
            return container.ID;
        }

        if (useHttpServerMode)
        {
            // ── HTTP server flow ─────────────────────────────────────────────────
            // Drive all session management via the REST API; git operations via docker exec.
            try
            {
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

                // Log the actual commit SHA after workspace setup (git clone via docker exec).
                // Also emit the git markers early so IssueWorker captures branch/SHA immediately.
                // EmitGitMarkersAsync runs after the agent completes and will overwrite these with
                // the final committed state, but emitting here ensures the session header is populated
                // even when the agent fails and EmitGitMarkersAsync never runs.
                if (cloneRepo is not null)
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

                // Signal the start of post-run operations so IssueWorker can tag subsequent
                // log lines (git state check, push, markers) with the PostRun section.
                await onLogLine(PostRunStartMarker, LogStream.Stdout);

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
        // The container is alive (running `tail -f /dev/null`). Drive all agent work via exec.
        try
        {
            // Step 6: Log the actual commit SHA after workspace setup (git clone via docker exec).
            // Also emit the git markers early so IssueWorker captures branch/SHA immediately.
            // EmitGitMarkersAsync runs after the agent completes and will overwrite these with
            // the final committed state, but emitting here ensures the session header is populated
            // even when the agent fails and EmitGitMarkersAsync never runs.
            if (cloneRepo is not null)
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
            // Prefer runnerArgs (set when RunnerType is configured), fall back to session.CustomCmd
            // (DockerCmdOverride for diagnostic/test runs). When neither is set the agent has no
            // RunnerType and no override — skip execution and treat the run as a no-op.
            // Working directory: use /workspace only when a repo was actually cloned; otherwise
            // fall back to / so the exec doesn't fail on images that don't have /workspace.
            IReadOnlyList<string>? effectiveCmd = runnerArgs.Count > 0 ? runnerArgs
                : (session.CustomCmd is { Length: > 0 } ? session.CustomCmd : null);
            var agentWorkingDir = cloneRepo is not null ? "/workspace" : "/";
            var agentExitCode = 0L;
            if (effectiveCmd is not null)
                agentExitCode = await ExecCommandAsync(container.ID, effectiveCmd, onLogLine, cancellationToken, logCommand: true, workingDir: agentWorkingDir);
            else
                await onLogLine("[WARN] No agent command to execute (agent has no RunnerType and no CustomCmd override -- session will complete as no-op)", LogStream.Stderr);

            // Step 8: Capture the opencode session ID for --fork on subsequent fix runs.
            // NOTE: opencode run --fork <session-id> will continue from the same session and retain
            // full conversation context. The same container already gives the agent access to the
            // git workspace as modified by the first run. --fork will be wired up once opencode
            // supports the flag in non-interactive (opencode run) mode.
            var postRunMarkerEmitted = false;
            if (agent.RunnerType == RunnerType.OpenCode)
            {
                // Signal the start of post-run operations so IssueWorker can tag logs with PostRun.
                await onLogLine(PostRunStartMarker, LogStream.Stdout);
                postRunMarkerEmitted = true;
                try { await CaptureOpenCodeSessionIdAsync(container.ID, onLogLine, cancellationToken); }
                catch (Exception ex)
                {
                    await onLogLine($"[WARN] opencode session list failed: {ex.Message}", LogStream.Stderr);
                }
            }

            // Step 9: Check git state and emit markers so IssueWorker can trigger CI/CD.
            if (gitRepository is not null)
            {
                if (!postRunMarkerEmitted)
                {
                    // Signal the start of post-run operations for non-OpenCode agents.
                    await onLogLine(PostRunStartMarker, LogStream.Stdout);
                }
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
            var exitCode = await ExecCommandAsync(containerId, runnerArgs, onFixLogLine, cancellationToken, logCommand: true);
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

    /// <summary>
    /// After a failed push, loops through <paramref name="allGitRepositories"/> in order, fetching
    /// the current branch from each remote and rebasing local commits on top. Once all reachable
    /// remotes are integrated, retries the push to <paramref name="gitRepository"/> (push target).
    /// Emits an updated <c>[ISSUEPIT:GIT_COMMIT_SHA]</c> marker when the retry succeeds.
    /// </summary>
    public async Task<bool> TryIntegrateRemotesAndRetryPushAsync(
        string containerId,
        GitRepository gitRepository,
        IReadOnlyList<GitRepository> allGitRepositories,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var realGit = (await ExecReadOutputAsync(
            containerId,
            ["/bin/sh", "-c", "cat /tmp/.issuepit-real-git 2>/dev/null || echo /usr/bin/git"],
            cancellationToken)).Trim();

        var branch = (await ExecReadOutputAsync(
            containerId, [realGit, "branch", "--show-current"], cancellationToken)).Trim();

        if (string.IsNullOrWhiteSpace(branch))
        {
            await onLogLine("[WARN] Multi-remote recovery: could not determine current branch.", LogStream.Stdout);
            return false;
        }

        // Collect ALL auth tokens from all configured remotes so every token can be redacted
        // from git output, not just the push target's token. When fetching from non-Working
        // remotes, git error messages may echo their authenticated URLs, leaking those tokens.
        var allTokens = allGitRepositories
            .Select(r => r.AuthToken)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Task safeLog(string line, LogStream stream)
        {
            var safeLine = line;
            foreach (var token in allTokens)
                safeLine = safeLine.Replace(token!, "***", StringComparison.Ordinal);
            return onLogLine($"[entrypoint] {safeLine}", stream);
        }

        // Loop through every configured remote. For each remote that has the branch,
        // fetch it and rebase local commits on top. Skip remotes whose fetch fails
        // (e.g. branch not yet on that remote) but abort on rebase conflicts.
        foreach (var repo in allGitRepositories)
        {
            var remoteUrl = BuildAuthenticatedCloneUrl(repo.RemoteUrl, repo.AuthUsername, repo.AuthToken);
            var safeModeLabel = repo.Mode.ToString();

            await onLogLine(
                $"[INFO] Fetching '{branch}' from {safeModeLabel} remote to integrate…",
                LogStream.Stdout);

            var fetchExit = await ExecCommandAsync(
                containerId,
                [realGit, "fetch", remoteUrl, branch],
                safeLog, cancellationToken,
                env: ["GIT_TERMINAL_PROMPT=0"]);

            if (fetchExit != 0)
            {
                // Branch not on this remote yet — not a fatal error, skip and continue.
                await onLogLine(
                    $"[DEBUG] Fetch from {safeModeLabel} failed (branch may not exist there) — skipping.",
                    LogStream.Stdout);
                continue;
            }

            var rebaseExit = await ExecCommandAsync(
                containerId,
                [realGit, "rebase", "FETCH_HEAD"],
                safeLog, cancellationToken);

            if (rebaseExit != 0)
            {
                // Rebase conflict — abort and surface as a failure so the user is aware.
                var abortExit = await ExecCommandAsync(containerId, [realGit, "rebase", "--abort"],
                    safeLog, cancellationToken);
                if (abortExit != 0)
                    await onLogLine(
                        $"[WARN] Rebase --abort returned exit code {abortExit} — workspace may be in an inconsistent state.",
                        LogStream.Stdout);
                await onLogLine(
                    $"[WARN] Rebase against {safeModeLabel} remote failed with conflicts — " +
                    "manual conflict resolution is required before pushing.",
                    LogStream.Stdout);
                return false;
            }

            await onLogLine($"[INFO] Rebase against {safeModeLabel} remote succeeded.", LogStream.Stdout);
        }

        // All remotes integrated — retry the push to the Working remote.
        var pushTarget = BuildAuthenticatedCloneUrl(gitRepository.RemoteUrl, gitRepository.AuthUsername, gitRepository.AuthToken);
        await onLogLine("[INFO] All remotes integrated — retrying push…", LogStream.Stdout);

        var retryPushExit = await ExecCommandAsync(
            containerId,
            [realGit, "push", pushTarget, branch],
            safeLog, cancellationToken,
            env: ["GIT_TERMINAL_PROMPT=0"]);

        if (retryPushExit == 0)
        {
            await onLogLine("[INFO] Push succeeded after multi-remote integration.", LogStream.Stdout);
            // Emit an updated SHA marker so IssueWorker captures the final commit (may differ after rebase).
            var newSha = (await ExecReadOutputAsync(containerId, [realGit, "rev-parse", "HEAD"], cancellationToken)).Trim();
            if (!string.IsNullOrWhiteSpace(newSha))
                await onLogLine($"{GitCommitShaMarker}{newSha}", LogStream.Stdout);
            return true;
        }

        await onLogLine("[WARN] Push still failed after multi-remote integration.", LogStream.Stdout);
        return false;
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
        // The container starts immediately with `tail -f /dev/null` so this should
        // return on the first poll.
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
            $"Check the session logs for errors (git clone failure, dockerd timeout, etc.).{logSection}",
            LogStream.Stderr);

        throw new InvalidOperationException(
            $"Container {containerId[..Math.Min(12, containerId.Length)]} exited with code {exitCode} during startup. " +
            $"Check the session logs for details.");
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
    /// Injects a backed-up opencode <c>auth.json</c> file into the container at
    /// <c>/root/.local/share/opencode/auth.json</c>.
    /// Called when <c>ISSUEPIT_AUTH_JSON_CONTENT</c> is set in the container environment,
    /// meaning a previously captured auth backup should be restored for this run.
    /// </summary>
    private async Task InjectAuthJsonAsync(
        string containerId,
        string authJsonContent,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        const string AuthJsonPath = "/root/.local/share/opencode";
        const string AuthJsonFileName = "auth.json";

        // Normalise line endings for the JSON file.
        var authBytes = System.Text.Encoding.UTF8.GetBytes(authJsonContent.Replace("\r\n", "\n").Replace("\r", "\n"));

        // Ensure the target directory exists inside the container.
        await ExecCommandAsync(containerId, ["/bin/sh", "-c", $"mkdir -p {AuthJsonPath}"],
            (_, _) => Task.CompletedTask, cancellationToken, workingDir: "/");

        using var tarBuffer = new MemoryStream();
        await using (var tarWriter = new TarWriter(tarBuffer, TarEntryFormat.Ustar, leaveOpen: true))
        {
            var entry = new UstarTarEntry(TarEntryType.RegularFile, AuthJsonFileName)
            {
                Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
                DataStream = new MemoryStream(authBytes),
            };
            await tarWriter.WriteEntryAsync(entry, cancellationToken);
        }

        tarBuffer.Seek(0, SeekOrigin.Begin);
        await dockerClient.Containers.ExtractArchiveToContainerAsync(
            containerId,
            new CopyToContainerParameters { Path = AuthJsonPath },
            tarBuffer,
            cancellationToken);

        await onLogLine("[INFO] auth.json restored from backup — GitHub authentication available", LogStream.Stdout);
    }

    /// <summary>
    /// Injects the <c>issuepit-trigger-cicd</c> script into the container at
    /// <c>/usr/local/bin/issuepit-trigger-cicd</c>.
    /// The script allows the user in a manual terminal session to push the current feature
    /// branch and trigger a CI/CD run with a single command, then waits for the result.
    ///
    /// Prerequisites in the container environment:
    ///   - <c>ISSUEPIT_API_URL</c>: base URL of the IssuePit API (e.g. http://host.docker.internal:5000)
    ///   - <c>ISSUEPIT_MCP_TOKEN</c>: ephemeral token accepted by the API via <c>X-Mcp-Token</c>
    ///   - <c>ISSUEPIT_SESSION_ID</c>: the agent session ID used to look up project/branch server-side
    /// </summary>
    private async Task InjectCiCdTriggerScriptAsync(
        string containerId,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        const string scriptContent = """
            #!/bin/sh
            # issuepit-trigger-cicd — commit (optional), push the current branch, trigger a
            # CI/CD run in IssuePit, wait for it to finish, and exit with the CI/CD result.
            #
            # Usage:
            #   issuepit-trigger-cicd              # push + trigger (requires clean working tree)
            #   issuepit-trigger-cicd --commit      # auto-commit all changes, then push + trigger
            #
            # Environment variables (injected by IssuePit on container startup):
            #   ISSUEPIT_API_URL      Base URL of the IssuePit API
            #   ISSUEPIT_MCP_TOKEN    Ephemeral auth token accepted by the API
            #   ISSUEPIT_SESSION_ID   Agent session ID used to resolve project / branch
            set -e
            cd /workspace 2>/dev/null || true

            AUTO_COMMIT=0
            if [ "$1" = "--commit" ]; then
              AUTO_COMMIT=1
            fi

            # 1. Fail if there are uncommitted changes (unless --commit was given).
            if [ "$AUTO_COMMIT" = "1" ]; then
              echo "[INFO] Staging all changes..."
              git add -A
              if ! git diff --cached --quiet; then
                git commit -m "chore: auto-commit before CI/CD trigger"
                echo "[INFO] Changes committed."
              else
                echo "[INFO] Nothing to commit."
              fi
            elif ! git diff --quiet || ! git diff --cached --quiet; then
              echo "[ERROR] Uncommitted changes detected. Commit or stash your changes first, or run with --commit."
              exit 1
            fi

            # 2. Push the current feature branch.
            echo "[INFO] Pushing branch '$(git branch --show-current)' to origin..."
            git push --set-upstream origin "$(git branch --show-current)"
            echo "[OK] Branch pushed."

            # 3. Trigger a CI/CD run via the IssuePit API.
            if [ -z "$ISSUEPIT_API_URL" ] || [ -z "$ISSUEPIT_MCP_TOKEN" ] || [ -z "$ISSUEPIT_SESSION_ID" ]; then
              echo "[WARN] ISSUEPIT_API_URL / ISSUEPIT_MCP_TOKEN / ISSUEPIT_SESSION_ID not set — skipping IssuePit CI/CD trigger."
              exit 0
            fi

            echo "[INFO] Triggering CI/CD run via IssuePit API..."
            TRIGGER_RESPONSE=$(curl -s -X POST \
              "$ISSUEPIT_API_URL/api/agent-sessions/$ISSUEPIT_SESSION_ID/trigger-cicd" \
              -H "X-Mcp-Token: $ISSUEPIT_MCP_TOKEN" \
              -H "Content-Type: application/json")

            RUN_ID=$(echo "$TRIGGER_RESPONSE" | grep -o '"runId":"[^"]*"' | cut -d'"' -f4)
            if [ -z "$RUN_ID" ]; then
              echo "[ERROR] Failed to trigger CI/CD run. API response:"
              echo "$TRIGGER_RESPONSE"
              exit 1
            fi

            echo "[INFO] CI/CD run queued: $RUN_ID"
            echo "[INFO] Tracking run at: $ISSUEPIT_API_URL/../projects/runs/cicd/$RUN_ID"

            # 4. Poll for completion (max 30 minutes, every 10 seconds).
            POLL_MAX=180
            POLL_COUNT=0
            while [ "$POLL_COUNT" -lt "$POLL_MAX" ]; do
              STATUS_RESPONSE=$(curl -s \
                "$ISSUEPIT_API_URL/api/cicd-runs/$RUN_ID" \
                -H "X-Mcp-Token: $ISSUEPIT_MCP_TOKEN")

              STATUS=$(echo "$STATUS_RESPONSE" | grep -o '"statusName":"[^"]*"' | cut -d'"' -f4)

              case "$STATUS" in
                Succeeded|SucceededWithWarnings)
                  echo "[OK] CI/CD run completed successfully (status: $STATUS)."
                  exit 0
                  ;;
                Failed|Cancelled|TimedOut)
                  echo "[FAIL] CI/CD run finished with status: $STATUS."
                  exit 1
                  ;;
                Pending|Running|WaitingForApproval)
                  printf "."
                  ;;
                *)
                  echo "[WARN] Unknown CI/CD status '$STATUS' — continuing to poll..."
                  ;;
              esac

              POLL_COUNT=$((POLL_COUNT + 1))
              sleep 10
            done

            echo ""
            echo "[WARN] Timed out waiting for CI/CD run $RUN_ID after 30 minutes."
            exit 1
            """;

        var scriptBytes = System.Text.Encoding.UTF8.GetBytes(scriptContent.Replace("\r\n", "\n").Replace("\r", "\n"));

        using var tarBuffer = new MemoryStream();
        await using (var tarWriter = new TarWriter(tarBuffer, TarEntryFormat.Ustar, leaveOpen: true))
        {
            var entry = new UstarTarEntry(TarEntryType.RegularFile, "issuepit-trigger-cicd")
            {
                Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                       | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                       | UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
                DataStream = new MemoryStream(scriptBytes),
            };
            await tarWriter.WriteEntryAsync(entry, cancellationToken);
        }

        tarBuffer.Seek(0, SeekOrigin.Begin);
        await dockerClient.Containers.ExtractArchiveToContainerAsync(
            containerId,
            new CopyToContainerParameters { Path = "/usr/local/bin" },
            tarBuffer,
            cancellationToken);

        await onLogLine("[INFO] CI/CD trigger script installed at /usr/local/bin/issuepit-trigger-cicd", LogStream.Stdout);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Docker exec helpers — provided by DockerRuntimeBase
    // ──────────────────────────────────────────────────────────────────────────
    // ExecCommandAsync, ExecReadOutputAsync, ReadMultiplexedStreamAsync,
    // StreamContainerLogsAsync, BuildAuthenticatedCloneUrl, StripOriginPrefix
    // are inherited from DockerRuntimeBase and available to all methods below.

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
    /// Runs <c>opencode session list</c> and returns <c>true</c> when <paramref name="sessionId"/>
    /// appears in the output. Used before passing <c>--session &lt;id&gt;</c> to <c>opencode run</c>
    /// to confirm the session exists in the restored DB and avoid a "Session not found" crash.
    /// Returns <c>false</c> if the session is not found or if the command fails.
    /// </summary>
    private async Task<bool> VerifyOpenCodeSessionExistsAsync(
        string containerId,
        string sessionId,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var found = false;
        try
        {
            await ExecCommandAsync(containerId, ["opencode", "session", "list"],
                (line, _) =>
                {
                    var trimmedLine = line.TrimStart();
                    var tokenEnd = trimmedLine.IndexOfAny([' ', '\t']);
                    var token = (tokenEnd > 0 ? trimmedLine[..tokenEnd] : trimmedLine).Trim();
                    if (string.Equals(token, sessionId, StringComparison.Ordinal))
                        found = true;
                    return Task.CompletedTask;
                }, cancellationToken);
        }
        catch (Exception ex)
        {
            await onLogLine($"[WARN] Could not verify opencode session existence: {ex.Message}", LogStream.Stderr);
        }
        return found;
    }

    /// <summary>
    /// Runs <c>opencode agent list</c> inside the container and logs the output at Debug level.
    /// Best-effort: failure is non-fatal and only logs a warning.
    /// Output is intentionally kept out of the user-visible session log (verbose diagnostics only).
    /// </summary>
    private async Task LogOpenCodeAgentsAsync(
        string containerId,
        CancellationToken cancellationToken)
    {
        // Collect output first so we can skip the header when there is nothing to show,
        // and silently suppress "executable not found" errors from test/minimal images.
        var lines = new List<string>();
        try
        {
            await ExecCommandAsync(containerId, ["opencode", "agent", "list"],
                (line, _) => { lines.Add(line); return Task.CompletedTask; },
                cancellationToken, workingDir: "/");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsExecutableNotFoundError(ex))
        {
            // opencode is not installed in this container (e.g. busybox test image) — skip silently.
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not list opencode agents in container {ContainerId}", containerId);
            return;
        }

        if (lines.Count == 0) return;

        logger.LogDebug("opencode agents (opencode agent list) in container {ContainerId}: {Lines}",
            containerId, string.Join("; ", lines));
    }

    /// <summary>
    /// Runs <c>opencode mcp list</c> inside the container and emits the output as log lines.
    /// Best-effort: failure is non-fatal and only logs a warning.
    /// Silently skipped when opencode is not installed (e.g. busybox test images).
    /// </summary>
    private async Task LogOpenCodeMcpServersAsync(
        string containerId,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        // Collect output first so we can skip the header when there is nothing to show,
        // and silently suppress "executable not found" errors from test/minimal images.
        var lines = new List<string>();
        try
        {
            await ExecCommandAsync(containerId, ["opencode", "mcp", "list"],
                (line, _) => { lines.Add(line); return Task.CompletedTask; },
                cancellationToken, workingDir: "/");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsExecutableNotFoundError(ex))
        {
            // opencode is not installed in this container (e.g. busybox test image) — skip silently.
            return;
        }
        catch (Exception ex)
        {
            await onLogLine($"[WARN] Could not list opencode MCP servers: {ex.Message}", LogStream.Stderr);
            return;
        }

        if (lines.Count == 0) return;

        await onLogLine("[INFO] opencode MCP servers (opencode mcp list):", LogStream.Stdout);
        foreach (var line in lines)
            await onLogLine($"[INFO]   {line}", LogStream.Stdout);
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="ex"/> indicates that the executable was not
    /// found inside the container (Docker OCI "executable file not found in $PATH" error).
    /// Used to silently skip diagnostic commands in minimal test images (e.g. busybox) that
    /// do not have opencode installed.
    /// </summary>
    /// <remarks>
    /// The check relies on the OCI runtime error message format produced by runc/containerd,
    /// which has been stable across all Linux Docker versions. The method requires that the
    /// exception is a <see cref="DockerApiException"/> to avoid matching unrelated errors.
    /// </remarks>
    private static bool IsExecutableNotFoundError(Exception ex) =>
        ex is DockerApiException &&
        (ex.Message.Contains("executable file not found", StringComparison.OrdinalIgnoreCase) ||
         ex.Message.Contains("no such file or directory", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Downloads each public issue attachment from its URL and injects the file content into the
    /// container at <c>/tmp/issuepit-attachments/&lt;filename&gt;</c>.
    /// Returns the list of container file paths that were successfully injected.
    /// Best-effort: individual download failures are logged and skipped.
    /// </summary>
    private async Task<IReadOnlyList<string>> DownloadAttachmentsToContainerAsync(
        string containerId,
        IList<IssueAttachment> attachments,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        const string attachmentDir = "/tmp/issuepit-attachments";

        await ExecCommandAsync(containerId, ["mkdir", "-p", attachmentDir],
            (_, _) => Task.CompletedTask, cancellationToken, workingDir: "/");

        var httpClient = httpClientFactory.CreateClient();
        var filePaths = new List<string>();

        foreach (var attachment in attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.FileUrl))
                continue;

            try
            {
                var content = await httpClient.GetByteArrayAsync(attachment.FileUrl, cancellationToken);

                // Sanitise the filename so it is safe to use as a container path component.
                var safeFileName = SanitizeAttachmentFileName(attachment.FileName);
                await InjectFileAsync(
                    containerId,
                    attachmentDir,
                    safeFileName,
                    content,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead,
                    cancellationToken);

                var containerPath = $"{attachmentDir}/{safeFileName}";
                filePaths.Add(containerPath);
                var sizeKb = content.Length / 1024.0;
                await onLogLine($"[INFO] Attachment injected: {safeFileName} ({sizeKb:F1} KB) → {containerPath}", LogStream.Stdout);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await onLogLine($"[WARN] Could not download attachment '{attachment.FileName}': {ex.Message}", LogStream.Stderr);
                logger.LogWarning(ex, "Failed to download attachment {FileName} from {Url} into container {ContainerId}",
                    attachment.FileName, attachment.FileUrl, containerId);
            }
        }

        if (filePaths.Count > 0)
            await onLogLine($"[INFO] {filePaths.Count} attachment(s) injected into {attachmentDir}", LogStream.Stdout);

        return filePaths;
    }

    /// <summary>
    /// Returns a sanitised version of an attachment filename that is safe to use as a container
    /// path component. Strips all characters invalid in filenames (including path separators and
    /// null bytes), guards against directory traversal sequences, and falls back to "attachment"
    /// when the name is empty after sanitisation.
    /// </summary>
    private static string SanitizeAttachmentFileName(string fileName)
    {
        // Remove all characters that are invalid in Unix filenames.
        var invalidChars = Path.GetInvalidFileNameChars();
        var safe = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray()).Trim();

        // Guard against directory traversal sequences (e.g. ".." or "...").
        if (safe == ".." || safe == ".")
            safe = string.Empty;

        return string.IsNullOrEmpty(safe) ? "attachment" : safe;
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
                // Use the real git binary path written by the entrypoint before it installed the
                // push-blocking wrapper at /usr/local/bin/git. This allows the execution client to
                // push branches via docker exec without hitting the wrapper meant to stop agents.
                var realGit = (await ExecReadOutputAsync(
                    containerId,
                    ["/bin/sh", "-c", "cat /tmp/.issuepit-real-git 2>/dev/null || echo /usr/bin/git"],
                    cancellationToken)).Trim();

                // Build the authenticated push target. Git 2.39+ no longer persists credentials
                // embedded in the clone URL to .git/config (it only uses them during clone), so a
                // subsequent `git push origin` would fail with "could not read Username" when no
                // credential helper is configured in the container. Passing the full authenticated
                // URL directly to git push avoids this and also ensures we push to the correct
                // Working-mode remote when multiple origins are configured.
                var pushTarget = gitRepository is not null
                    ? BuildAuthenticatedCloneUrl(gitRepository.RemoteUrl, gitRepository.AuthUsername, gitRepository.AuthToken)
                    : "origin";

                // Log the actual push target (redact credentials) for debugging.
                var tokenToRedact = gitRepository?.AuthToken;
                var safeTarget = !string.IsNullOrEmpty(tokenToRedact)
                    ? pushTarget.Replace(tokenToRedact, "***", StringComparison.Ordinal)
                    : pushTarget;
                var modeLabel = gitRepository is not null ? $" ({gitRepository.Mode})" : "";
                await onLogLine($"[entrypoint] Pushing branch '{branch}' to {safeTarget}{modeLabel}…", LogStream.Stdout);
                if (gitRepository is not null)
                    await onLogLine($"[DEBUG] Push hasCredentials={!string.IsNullOrEmpty(gitRepository.AuthToken)}", LogStream.Stdout);

                // Log available git remotes inside the container for diagnostics.
                // Redact the auth token so the URL that was embedded in the clone URL doesn't leak.
                var gitRemotes = await ExecReadOutputAsync(
                    containerId,
                    ["/bin/sh", "-c", "git remote -v 2>/dev/null || echo '(no remotes)'"],
                    cancellationToken);
                foreach (var remoteLine in gitRemotes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var safeRemoteLine = !string.IsNullOrEmpty(tokenToRedact)
                        ? remoteLine.Replace(tokenToRedact, "***", StringComparison.Ordinal)
                        : remoteLine;
                    await onLogLine($"[DEBUG] in-container remote: {safeRemoteLine}", LogStream.Stdout);
                }

                // Sanitize push output: git prints "To <url>" in its output, which would leak the
                // auth token when credentials are embedded in the push URL.
                Task safeLogLine(string line, LogStream stream)
                {
                    var safeLine = !string.IsNullOrEmpty(tokenToRedact)
                        ? line.Replace(tokenToRedact, "***", StringComparison.Ordinal)
                        : line;
                    return onLogLine($"[entrypoint] {safeLine}", stream);
                }

                var pushExit = await ExecCommandAsync(containerId, [realGit, "push", pushTarget, branch],
                    safeLogLine,
                    cancellationToken,
                    env: ["GIT_TERMINAL_PROMPT=0"]);
                if (pushExit != 0)
                {
                    await onLogLine(
                        "[entrypoint] Push failed (allowed — credentials may not be configured or push was rejected)",
                        LogStream.Stdout);
                    // Log a diagnostic hint when pushing to a URL that differs from the in-container origin.
                    // This is the cross-remote push scenario (Working ≠ clone source): the most common
                    // failure mode is a shallow clone whose parent objects the working remote does not have.
                    var inContainerOrigin = await ExecReadOutputAsync(
                        containerId,
                        ["/bin/sh", "-c", "git remote get-url origin 2>/dev/null || echo ''"],
                        cancellationToken);
                    var originUrl = inContainerOrigin.Trim().TrimEnd('/');
                    var pushUrl = gitRepository?.RemoteUrl?.TrimEnd('/');
                    if (!string.IsNullOrEmpty(pushUrl) && !string.IsNullOrEmpty(originUrl)
                        && !originUrl.Equals(pushUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        await onLogLine(
                            $"[DEBUG] Push target ({pushUrl}) differs from in-container origin ({originUrl}). " +
                            "If the error above is 'remote: fatal: did not receive expected object', " +
                            "this is a shallow-clone/cross-remote push failure. " +
                            "Verify the working remote has a base branch in common with the clone source, " +
                            "or ensure both remotes share the same git history.",
                            LogStream.Stdout);
                    }
                    // Emit a structured marker so IssueWorker can trigger multi-remote recovery and,
                    // if recovery fails, a .git archive upload.
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
    // Container log streaming — provided by DockerRuntimeBase
    // ──────────────────────────────────────────────────────────────────────────
    // StreamContainerLogsAsync and ReadMultiplexedStreamAsync are inherited from
    // DockerRuntimeBase. The private LogBufferSize and local Encoding.UTF8 usages
    // in this file are now served by the base class constant.

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

    /// <summary>
    /// Inspects the local Docker image and returns the first repo digest (e.g. <c>sha256:abc123…</c>),
    /// or <c>null</c> when the image is not present locally or inspection fails.
    /// </summary>
    private async Task<string?> TryGetImageDigestAsync(string image, CancellationToken cancellationToken)
    {
        try
        {
            var inspect = await dockerClient.Images.InspectImageAsync(image, cancellationToken);
            // RepoDigests contains entries like "ghcr.io/org/image@sha256:abc123..."
            // Return the digest portion after "@" for readability.
            var fullDigest = inspect.RepoDigests?.FirstOrDefault();
            if (string.IsNullOrEmpty(fullDigest)) return null;
            var atIndex = fullDigest.IndexOf('@');
            return atIndex >= 0 ? fullDigest[(atIndex + 1)..] : fullDigest;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not inspect image {Image} to retrieve digest", image);
            return null;
        }
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
