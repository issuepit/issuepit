using System.Reflection;
using System.Text;
using System.Text.Json;
using Docker.DotNet;
using Docker.DotNet.Models;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.CiCdClient.Runtimes;

/// <summary>
/// Runs <c>act</c> inside a Docker container, mounting the workspace as a volume.
/// This is the default CI/CD runtime.
///
/// Reads from:
/// <list type="bullet">
///   <item><c>CiCd__Docker__Image</c> — Docker image that has <c>act</c> installed
///     (default: <c>ghcr.io/issuepit/issuepit-helper-act:latest</c>)</item>
///   <item><c>CiCd__ActBinaryPath</c> — path to <c>act</c> inside the container (default: <c>act</c>)</item>
///   <item><c>CiCd__DefaultWorkspacePath</c> — fallback host path to the repository workspace</item>
///   <item><c>CiCd__ActImage</c> — runner image injected into <c>actrc</c> on first run to prevent
///     the interactive image-selection prompt (default: <c>catthehacker/ubuntu:act-latest</c> — Medium)</item>
///   <item><c>CiCd__Docker__DindCacheStrategy</c> — DinD image cache strategy:
///     <c>Off</c> | <c>LocalVolume</c> (default) | <c>RegistryMirror</c></item>
///   <item><c>CiCd__Docker__DindCacheVolumePath</c> — host path mounted as <c>/var/lib/docker</c> inside
///     DinD containers (default: <c>/var/lib/issuepit-dind-cache</c>); used by
///     <c>LocalVolume</c> and <c>RegistryMirror</c> strategies</item>
///   <item><c>CiCd__Docker__RegistryMirrorPort</c> — host port for the pull-through registry mirror
///     container (default: <c>5100</c>); used by the <c>RegistryMirror</c> strategy</item>
///   <item><c>CiCd__Docker__RegistryMirrorVolumePath</c> — host path for registry data
///     (default: <c>/var/lib/issuepit-registry-cache</c>); used by the <c>RegistryMirror</c> strategy</item>
/// </list>
/// </summary>
public partial class DockerCiCdRuntime(
    ILogger<DockerCiCdRuntime> logger,
    DockerClient dockerClient,
    IConfiguration configuration) : ICiCdRuntime
{
    // Docker image used to run act. Uses the IssuePit helper-act image which includes
    // .NET SDK, Node.js, Playwright, Docker CLI, and act pre-installed.
    //private const string DefaultImage = "ghcr.io/issuepit/issuepit-helper-act:latest";
    private const string DefaultImage = "ghcr.io/issuepit/issuepit-helper-act:main-dotnet10-node24";

    // Default DinD image cache settings.
    private const DindImageCacheStrategy DefaultDindCacheStrategy = DindImageCacheStrategy.LocalVolume;
    private const string DefaultDindCacheVolumePath = "/var/lib/issuepit-dind-cache";
    private const string RegistryMirrorContainerName = "issuepit-registry-mirror";
    private const int DefaultRegistryMirrorPort = 5100;
    private const string DefaultRegistryMirrorVolumePath = "/var/lib/issuepit-registry-cache";

    private static string AppVersion =>
        Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

    // Builds a unique Docker container name for a CI/CD run.
    // A random UUID is used (instead of deriving from the run ID) so that retries and parallel
    // runs never produce a name conflict even when a previous container wasn't cleaned up yet.
    // The run ID is always available via the "issuepit.run-id" container label.
    private static string BuildContainerName(CiCdRun run) =>
        $"issuepit-cicd-{Guid.NewGuid():N}"[..24];

    public async Task RunAsync(
        CiCdRun run,
        TriggerPayload trigger,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var image = !string.IsNullOrWhiteSpace(trigger.CustomImage)
            ? trigger.CustomImage
            : configuration["CiCd__Docker__Image"] ?? DefaultImage;
        var actBin = configuration["CiCd__ActBinaryPath"] ?? "act";
        var workspacePath = trigger.WorkspacePath ?? configuration["CiCd__DefaultWorkspacePath"];

        // Workspace path is required unless volume mounts are disabled OR a git repo URL is set
        // (in which case the repo is cloned inside the container — no host path needed).
        var hasGitRepo = !string.IsNullOrWhiteSpace(trigger.GitRepoUrl);
        if (!trigger.NoVolumeMounts && !hasGitRepo &&
            (string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath)))
            throw new InvalidOperationException(
                $"Workspace path '{workspacePath}' is not configured or does not exist. " +
                "Set CiCd__DefaultWorkspacePath to the repository workspace, or supply a GitRepoUrl.");

        var actArgs = NativeCiCdRuntime.BuildActArgumentsList(trigger);
        var actBinAndArgs = new[] { actBin }.Concat(actArgs).ToList();

        // For the Docker runtime the artifact server path in trigger is a host path.
        // Replace it with the container-internal path so act stores artifacts inside the container
        // (which is mounted from the host directory, making files accessible on both sides).
        const string ContainerArtifactPath = "/artifacts";
        if (!string.IsNullOrWhiteSpace(trigger.ArtifactServerPath))
        {
            var idx = actBinAndArgs.IndexOf(trigger.ArtifactServerPath);
            if (idx >= 0)
                actBinAndArgs[idx] = ContainerArtifactPath;
        }

        // Append custom CLI args if provided (e.g. "--verbose --reuse").
        // Note: args are split on spaces; quoted arguments with spaces are not supported.
        if (!string.IsNullOrWhiteSpace(trigger.CustomArgs))
        {
            foreach (var a in trigger.CustomArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                actBinAndArgs.Add(a);
        }

        var containerName = BuildContainerName(run);

        // Read the act runner image that is injected into actrc to prevent the interactive
        // first-run prompt ("Please choose the default image") that causes EOF in non-interactive containers.
        // Default is the Medium runner image (~500 MB, compatible with most actions).
        // Priority: trigger (project/org override) → CiCd__ActImage config → hardcoded default.
        var actRunnerImage = !string.IsNullOrWhiteSpace(trigger.ActRunnerImage)
            ? trigger.ActRunnerImage
            : configuration["CiCd__ActImage"] ?? "catthehacker/ubuntu:act-latest";

        // Build -P platform flags appended to the act command so the prompt is suppressed
        // even if the actrc isn't read (stale image layer, wrong XDG_CONFIG_HOME, etc.).
        var platformLabels = new[] { "ubuntu-latest", "ubuntu-24.04", "ubuntu-22.04", "ubuntu-20.04" };
        var platformCmdArgs = platformLabels.SelectMany(label => new[] { "-P", $"{label}={actRunnerImage}" });
        var actCmd = actBinAndArgs.Concat(platformCmdArgs).ToList();

        // Emit verbose diagnostics as the first log lines so they appear in the run's log output.
        await onLogLine($"[DEBUG] Runner machine : {Environment.MachineName}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Runtime        : Docker (exec model)", LogStream.Stdout);
        await onLogLine($"[DEBUG] IssuePit ver   : {AppVersion}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Docker image   : {image}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Act runner img : {actRunnerImage}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Container name : {containerName}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Command        : {string.Join(' ', actCmd)}", LogStream.Stdout);
        if (hasGitRepo)
            await onLogLine($"[DEBUG] Git repo URL   : {trigger.GitRepoUrl}", LogStream.Stdout);
        else if (!string.IsNullOrWhiteSpace(workspacePath))
            await onLogLine($"[DEBUG] Workspace      : {workspacePath}", LogStream.Stdout);
        if (!trigger.NoVolumeMounts && !hasGitRepo)
            await onLogLine($"[DEBUG] Mount          : {workspacePath}:/workspace", LogStream.Stdout);
        await onLogLine($"[DEBUG] Working dir    : /workspace", LogStream.Stdout);
        if (trigger.NoDind) await onLogLine($"[DEBUG] DinD           : disabled", LogStream.Stdout);
        else await onLogLine($"[DEBUG] DinD           : isolated (Privileged=true, in-container dockerd)", LogStream.Stdout);
        if (trigger.NoVolumeMounts) await onLogLine($"[DEBUG] Volume mounts  : disabled", LogStream.Stdout);
        if (!string.IsNullOrWhiteSpace(trigger.CustomEntrypoint))
            await onLogLine($"[DEBUG] Entrypoint     : {trigger.CustomEntrypoint}", LogStream.Stdout);

        // Resolve the DinD image cache strategy.
        // Priority: trigger override → CiCd__Docker__DindCacheStrategy config → hardcoded default (LocalVolume).
        // Cache is only meaningful when DinD is active; force Off when NoDind=true.
        DindImageCacheStrategy effectiveCacheStrategy;
        if (trigger.NoDind)
        {
            effectiveCacheStrategy = DindImageCacheStrategy.Off;
        }
        else if (trigger.DindCacheStrategy.HasValue)
        {
            effectiveCacheStrategy = trigger.DindCacheStrategy.Value;
        }
        else if (Enum.TryParse<DindImageCacheStrategy>(configuration["CiCd__Docker__DindCacheStrategy"], ignoreCase: true, out var configStrategy))
        {
            effectiveCacheStrategy = configStrategy;
        }
        else
        {
            effectiveCacheStrategy = DefaultDindCacheStrategy;
        }

        await onLogLine($"[DEBUG] Cache strategy : {effectiveCacheStrategy}", LogStream.Stdout);

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

        logger.LogInformation("Pulling Docker image {Image} for CI/CD run {RunId}", image, run.Id);
        var pullStart = DateTime.UtcNow;
        await onLogLine($"[DEBUG] Pull started   : {pullStart:u}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Pulling image  : {image}", LogStream.Stdout);
        try
        {
            await dockerClient.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = image },
                null,
                // Progress handler is required by the API but pull status is captured via container logs
                new Progress<JSONMessage>(),
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            var msg = $"Lost connection to the Docker daemon while pulling image '{image}'. " +
                "This can happen on Windows when Docker Desktop resets named-pipe connections. " +
                "Try running the CI/CD run again.";
            await onLogLine($"[ERROR] {msg}", LogStream.Stderr);
            foreach (var line in ex.ToString().Split('\n'))
                await onLogLine(line.TrimEnd('\r'), LogStream.Stderr);
            throw new InvalidOperationException(msg, ex);
        }

        var pullDuration = DateTime.UtcNow - pullStart;
        await onLogLine(
            $"[DEBUG] Pull finished  : {DateTime.UtcNow:u} (took {pullDuration.TotalSeconds:F1}s)",
            LogStream.Stdout);

        logger.LogInformation("Creating Docker container from image {Image} for CI/CD run {RunId}", image, run.Id);

        // Build bind mounts based on trigger options.
        // When a git repo URL is set the workspace is cloned inside the container, so no host volume is needed.
        // True DinD (Privileged=true + in-container dockerd) is used by default — the host docker socket
        // is never mounted, keeping the host daemon fully isolated from CI/CD container activity.
        var binds = new List<string>();
        if (!trigger.NoVolumeMounts && !hasGitRepo)
        {
            binds.Add($"{workspacePath}:/workspace");
        }

        // Mount the artifact server directory from the host so artifacts are accessible after the run.
        // ArtifactServerPath is always an absolute temp-directory path set by the CiCdWorker
        // (Path.Combine(Path.GetTempPath(), "issuepit-artifacts-{runId}")), so it is safe to use directly.
        if (!string.IsNullOrWhiteSpace(trigger.ArtifactServerPath))
        {
            Directory.CreateDirectory(trigger.ArtifactServerPath);
            binds.Add($"{trigger.ArtifactServerPath}:{ContainerArtifactPath}");
            await onLogLine($"[DEBUG] Artifact mount : {trigger.ArtifactServerPath}:{ContainerArtifactPath}", LogStream.Stdout);
        }

        // Apply the DinD image cache strategy: add volume mounts and/or start the registry mirror.
        string? registryMirrorUrl = null;
        var extraHosts = new List<string>();

        if (effectiveCacheStrategy is DindImageCacheStrategy.LocalVolume or DindImageCacheStrategy.RegistryMirror)
        {
            var cacheVolumePath = configuration["CiCd__Docker__DindCacheVolumePath"] ?? DefaultDindCacheVolumePath;
            Directory.CreateDirectory(cacheVolumePath);
            binds.Add($"{cacheVolumePath}:/var/lib/docker");
            await onLogLine($"[DEBUG] DinD cache vol : {cacheVolumePath}:/var/lib/docker", LogStream.Stdout);
        }

        if (effectiveCacheStrategy == DindImageCacheStrategy.RegistryMirror)
        {
            var mirrorPort = int.TryParse(configuration["CiCd__Docker__RegistryMirrorPort"], out var p) ? p : DefaultRegistryMirrorPort;
            var mirrorVolumePath = configuration["CiCd__Docker__RegistryMirrorVolumePath"] ?? DefaultRegistryMirrorVolumePath;

            try
            {
                await EnsureRegistryMirrorAsync(mirrorPort, mirrorVolumePath, onLogLine, cancellationToken);
                // Use host.docker.internal so the DinD dockerd inside the container can reach the registry
                // on the host. Docker 20.10+ resolves this to the host gateway IP on Linux.
                registryMirrorUrl = $"http://host.docker.internal:{mirrorPort}";
                extraHosts.Add("host.docker.internal:host-gateway");
                await onLogLine($"[DEBUG] Registry mirror: {registryMirrorUrl}", LogStream.Stdout);
            }
            catch (Exception ex)
            {
                // Degraded mode: log a warning and continue without the mirror (cache miss, not a failure).
                await onLogLine($"[WARN] Registry mirror unavailable, falling back to LocalVolume cache: {ex.Message}", LogStream.Stderr);
                registryMirrorUrl = null;
            }
        }

        // When a custom entrypoint is set the caller controls execution; use their entrypoint+cmd directly.
        // Otherwise use the exec model: start a long-running shell, then exec each step one by one.
        var useExecModel = string.IsNullOrWhiteSpace(trigger.CustomEntrypoint);

        var createParams = new CreateContainerParameters
        {
            Image = image,
            Name = containerName,
            // Exec model: keep the container alive with a sleep shell so we can exec steps into it.
            // Custom entrypoint: let the caller's cmd run directly.
            Cmd = useExecModel
                ? ["tail -f /dev/null"]
                : actCmd,
            WorkingDir = "/workspace",
            Entrypoint = !useExecModel
                ? trigger.CustomEntrypoint!.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                : ["/bin/sh", "-c"],
            HostConfig = new HostConfig
            {
                Binds = binds,
                AutoRemove = false,
                // Privileged mode is required for true DinD (in-container dockerd).
                // The host Docker socket is never mounted — act's job containers run inside
                // the container's own isolated daemon, fully isolated from the host.
                Privileged = !trigger.NoDind,
                ExtraHosts = extraHosts.Count > 0 ? extraHosts : null,
            },
            Labels = new Dictionary<string, string>
            {
                ["issuepit.run-id"] = run.Id.ToString(),
                ["issuepit.project-id"] = run.ProjectId.ToString(),
            },
        };

        CreateContainerResponse container;
        try
        {
            container = await CreateContainerWithRetryAsync(createParams, containerName, onLogLine, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException ||
            (ex is OperationCanceledException oce && oce.CancellationToken != cancellationToken && !cancellationToken.IsCancellationRequested))
        {
            var msg = "Lost connection to the Docker daemon while creating the container. " +
                "This can happen on Windows when Docker Desktop resets named-pipe connections. " +
                "Try running the CI/CD run again.";
            await onLogLine($"[ERROR] {msg}", LogStream.Stderr);
            foreach (var line in ex.ToString().Split('\n'))
                await onLogLine(line.TrimEnd('\r'), LogStream.Stderr);
            throw new InvalidOperationException(msg, ex);
        }

        logger.LogInformation("Created Docker container {ContainerId} ({ContainerName}) for CI/CD run {RunId}",
            container.ID, containerName, run.Id);
        await onLogLine($"[DEBUG] Container ID   : {container.ID[..12]}", LogStream.Stdout);

        var succeeded = false;
        try
        {
            await StartContainerWithRetryAsync(container.ID, actBin, image, containerName, onLogLine, cancellationToken);

            if (useExecModel)
            {
                // ── Exec model: run each step inside the container sequentially ─────────────

                // Dynamically compute the number of steps so the [N/M] prefix is always correct.
                var useDind = !trigger.NoDind;
                var totalSteps = 2 + (useDind ? 1 : 0) + (hasGitRepo ? 1 : 0) + (!string.IsNullOrWhiteSpace(trigger.Workflow) ? 1 : 0);
                var stepNum = 0;

                // Step: Start dockerd (true DinD — no host socket mount).
                // The first exec step starts the in-container daemon and waits until it is ready.
                if (useDind)
                {
                    await onLogLine($"[DEBUG] Step {++stepNum}/{totalSteps}: starting dockerd (DinD)", LogStream.Stdout);
                    var dindExitCode = await ExecCommandAsync(
                        container.ID,
                        ["/bin/sh", "-c", BuildDindStartupScript(registryMirrorUrl)],
                        onLogLine,
                        cancellationToken);
                    if (dindExitCode != 0)
                        throw new Exception($"dockerd failed to start inside the container (exit code {dindExitCode})");
                }

                // Step: git clone (when a repo URL is provided instead of a volume mount).
                if (hasGitRepo)
                {
                    await onLogLine($"[DEBUG] Step {++stepNum}/{totalSteps}: git clone {trigger.GitRepoUrl}", LogStream.Stdout);
                    var cloneExitCode = await ExecCommandAsync(
                        container.ID,
                        // Clone into /workspace so act can find the repo at its expected path.
                        ["git", "clone", "--depth=1", trigger.GitRepoUrl!, "/workspace"],
                        onLogLine,
                        cancellationToken);
                    if (cloneExitCode != 0)
                        throw new Exception($"git clone failed with exit code {cloneExitCode} for URL '{trigger.GitRepoUrl}'");

                    // Best-effort: copy workflow files to the artifact directory so the worker can
                    // generate the workflow graph after the run without needing a local workspace.
                    if (!string.IsNullOrWhiteSpace(trigger.ArtifactServerPath))
                    {
                        await ExecShellAsync(
                            container.ID,
                            "if [ -d /workspace/.github/workflows ]; then cp -r /workspace/.github/workflows/ /artifacts/_workflows/; fi",
                            onLogLine,
                            cancellationToken);
                    }
                }

                // Step: Write actrc to suppress the interactive image-selection prompt.
                // Use 'printf %b' so that the leading '-P' in the content is not misinterpreted
                // as a printf option by /bin/sh (dash).
                await onLogLine($"[DEBUG] Step {++stepNum}/{totalSteps}: writing actrc", LogStream.Stdout);
                await ExecShellAsync(container.ID, BuildActrcSetupScript(actRunnerImage), onLogLine, cancellationToken);

                // Step: Validate the workflow with actionlint (best-effort — never aborts the run).
                if (!string.IsNullOrWhiteSpace(trigger.Workflow))
                {
                    await onLogLine($"[DEBUG] Step {++stepNum}/{totalSteps}: actionlint validation", LogStream.Stdout);
                    await ExecShellAsync(container.ID, BuildActionlintExecScript(trigger.Workflow), onLogLine, cancellationToken);
                }

                // Step: Run act.
                await onLogLine($"[DEBUG] Step {++stepNum}/{totalSteps}: {string.Join(' ', actCmd)}", LogStream.Stdout);
                var actExitCode = await ExecCommandAsync(container.ID, actCmd, onLogLine, cancellationToken);

                if (actExitCode != 0)
                    throw new Exception(
                        $"act exited with code {actExitCode} " +
                        $"(image: {image}, event: {trigger.EventName ?? "push"}, workflow: {trigger.Workflow ?? "default"})");
            }
            else
            {
                // Custom entrypoint: stream container logs and wait for it to exit.
                var logStreamTask = StreamContainerLogsAsync(container.ID, onLogLine, cancellationToken);
                var waitResponse = await dockerClient.Containers.WaitContainerAsync(container.ID, cancellationToken);
                await logStreamTask;

                if (waitResponse.StatusCode != 0)
                    throw new Exception(
                        $"act exited with code {waitResponse.StatusCode} " +
                        $"(image: {image}, event: {trigger.EventName ?? "push"}, workflow: {trigger.Workflow ?? "default"})");
            }

            succeeded = true;
        }
        catch (OperationCanceledException)
        {
            // Kill the container when cancellation is requested.
            try
            {
                await dockerClient.Containers.KillContainerAsync(
                    container.ID, new ContainerKillParameters(), CancellationToken.None);
            }
            catch { /* best-effort */ }
            throw;
        }
        finally
        {
            if (!succeeded)
                await EmitFailureDiagnosticsAsync(container.ID, image, run, onLogLine);

            var keepContainer = !succeeded && trigger.KeepContainerOnFailure;
            if (keepContainer)
            {
                await onLogLine(
                    $"[DEBUG] Container kept : {container.ID[..12]} (name: {containerName}, KeepContainerOnFailure=true)" +
                    $" — run `docker ps -a` to find it, `docker exec -it {containerName} sh` to inspect",
                    LogStream.Stdout);
                logger.LogInformation(
                    "Keeping Docker container {ContainerId} ({ContainerName}) for failed CI/CD run {RunId} (KeepContainerOnFailure=true)",
                    container.ID, containerName, run.Id);
            }
            else
            {
                try
                {
                    await dockerClient.Containers.RemoveContainerAsync(
                        container.ID,
                        new ContainerRemoveParameters { Force = true },
                        CancellationToken.None);
                }
                catch { /* best-effort */ }
            }
        }
    }

    /// <summary>
    /// Creates a Docker container with retry logic that handles both connection-reset errors and
    /// name-conflict (409) errors. A name conflict occurs when a previous attempt succeeded on
    /// the Docker daemon side but the HTTP response was lost (named-pipe reset on Windows):
    /// the container was created but we never received its ID, so on retry the same name is
    /// rejected. The fix is to use a fresh unique name on every retry attempt so that any
    /// orphaned container from a previous attempt is simply abandoned (it will be cleaned up
    /// by the issuepit.run-id label at the end of the run, or by periodic Docker pruning).
    /// </summary>
    private async Task<CreateContainerResponse> CreateContainerWithRetryAsync(
        CreateContainerParameters createParams,
        string containerName,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken,
        int maxAttempts = 3)
    {
        // Loop exits via 'return' on success, or when the exception filter (attempt < maxAttempts)
        // evaluates to false on the final attempt — allowing the exception to propagate naturally.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await dockerClient.Containers.CreateContainerAsync(createParams, cancellationToken);
            }
            catch (Exception ex) when (
                attempt < maxAttempts &&
                !cancellationToken.IsCancellationRequested &&
                (ex is HttpRequestException or IOException ||
                 (ex is OperationCanceledException oce && oce.CancellationToken != cancellationToken) ||
                 (ex is DockerApiException dex && dex.StatusCode == System.Net.HttpStatusCode.Conflict)))
            {
                var reason = ex is DockerApiException ? "name conflict" : "connection reset";
                // Generate a fresh unique name so that any orphaned container from the previous
                // attempt does not cause another Conflict on the next attempt.
                var newName = $"issuepit-cicd-{Guid.NewGuid():N}"[..24];
                createParams.Name = newName;
                await onLogLine(
                    $"[WARN] CreateContainer: {reason} (attempt {attempt}/{maxAttempts}), retrying with new name '{newName}' in 2s…",
                    LogStream.Stderr);

                await Task.Delay(2000, cancellationToken);
                // Verify daemon is still reachable before retrying.
                await dockerClient.System.PingAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// Starts a container with retry/inspect logic that handles connection-reset errors.
    /// After a transient error, inspects the container state:
    /// if it is already "running", the start succeeded (response was lost); otherwise retries.
    /// On hard failures (e.g. missing binary) the method throws immediately.
    /// </summary>
    private async Task StartContainerWithRetryAsync(
        string containerId,
        string actBin,
        string image,
        string containerName,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken,
        int maxAttempts = 3)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await dockerClient.Containers.StartContainerAsync(
                    containerId, new ContainerStartParameters(), cancellationToken);
                // StartContainerAsync succeeded — container is starting.
                return;
            }
            catch (DockerApiException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.BadRequest &&
                ex.ResponseBody?.Contains("executable file not found") == true)
            {
                throw new InvalidOperationException(
                    $"The '{actBin}' binary was not found inside the Docker container. " +
                    $"Ensure the image '{image}' has 'act' installed, or override CiCd__ActBinaryPath " +
                    "with the correct path to the act binary inside the container.", ex);
            }
            catch (Exception ex) when (
                !cancellationToken.IsCancellationRequested &&
                (ex is HttpRequestException or IOException ||
                 (ex is OperationCanceledException oce && oce.CancellationToken != cancellationToken)))
            {
                // Connection reset or internal HTTP timeout. Inspect the container to determine
                // whether Docker actually started it (response was lost) or not.
                try
                {
                    var inspect = await dockerClient.Containers.InspectContainerAsync(
                        containerId, CancellationToken.None);
                    if (inspect.State?.Running == true || inspect.State?.Status == "running")
                    {
                        await onLogLine(
                            $"[WARN] StartContainer: connection reset but container is running — proceeding",
                            LogStream.Stdout);
                        return;
                    }
                }
                catch { /* inspection failed — fall through to retry */ }

                if (attempt >= maxAttempts)
                {
                    var startMsg = "Lost connection to the Docker daemon while starting the container. " +
                        $"The container '{containerName}' (ID {containerId[..12]}) may be in Created state. " +
                        $"You can start it manually: `docker start {containerName}`, then inspect with `docker exec -it {containerName} sh`. " +
                        "Or retry the run.";
                    await onLogLine($"[ERROR] {startMsg}", LogStream.Stderr);
                    foreach (var line in ex.ToString().Split('\n'))
                        await onLogLine(line.TrimEnd('\r'), LogStream.Stderr);
                    throw new InvalidOperationException(startMsg, ex);
                }

                await onLogLine(
                    $"[WARN] StartContainer: connection reset (attempt {attempt}/{maxAttempts}), retrying in 2s…",
                    LogStream.Stderr);
                await Task.Delay(2000, cancellationToken);
                await dockerClient.System.PingAsync(cancellationToken);
            }
        }
    }


    private async Task<T> RetryDockerAsync<T>(
        Func<Task<T>> operation,
        string context,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken,
        int maxAttempts = 3)
    {
        // Loop exits via 'return' on success, or when the exception filter (attempt < maxAttempts)
        // evaluates to false on the final attempt — allowing the exception to propagate naturally.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (
                attempt < maxAttempts &&
                !cancellationToken.IsCancellationRequested &&
                (ex is HttpRequestException or IOException ||
                 (ex is OperationCanceledException oce && oce.CancellationToken != cancellationToken)))
            {
                await onLogLine(
                    $"[WARN] {context}: connection reset (attempt {attempt}/{maxAttempts}), retrying in 2s…",
                    LogStream.Stderr);
                await Task.Delay(2000, cancellationToken);
                // Verify daemon is still reachable before retrying.
                await dockerClient.System.PingAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// Emits post-failure diagnostic information from the Docker daemon into the run log.
    /// All operations are best-effort and never throw.
    /// </summary>
    private async Task EmitFailureDiagnosticsAsync(
        string containerId,
        string image,
        CiCdRun run,
        Func<string, LogStream, Task> onLogLine)
    {
        try
        {
            await onLogLine("[DEBUG] --- Failure diagnostics ---", LogStream.Stdout);

            // Verify whether the image is present locally.
            try
            {
                var images = await dockerClient.Images.ListImagesAsync(
                    new ImagesListParameters
                    {
                        Filters = new Dictionary<string, IDictionary<string, bool>>
                        {
                            ["reference"] = new Dictionary<string, bool> { [image] = true },
                        },
                    },
                    CancellationToken.None);
                await onLogLine($"[DEBUG] Image present  : {(images.Count > 0 ? $"yes ({images.Count} tag(s))" : "no — image may not have been pulled correctly")}", LogStream.Stdout);
            }
            catch { /* best-effort */ }

            // Inspect the container if we have an ID (may already be removed).
            try
            {
                var inspect = await dockerClient.Containers.InspectContainerAsync(containerId, CancellationToken.None);
                await onLogLine($"[DEBUG] Container state: {inspect.State?.Status}, exit code: {inspect.State?.ExitCode}, error: {inspect.State?.Error}", LogStream.Stdout);
            }
            catch { /* best-effort — container may have been removed already */ }

            // List recent issuepit containers for context (docker ps -a with label filter).
            try
            {
                var containers = await dockerClient.Containers.ListContainersAsync(
                    new ContainersListParameters
                    {
                        All = true,
                        Filters = new Dictionary<string, IDictionary<string, bool>>
                        {
                            ["label"] = new Dictionary<string, bool>
                            {
                                [$"issuepit.project-id={run.ProjectId}"] = true,
                            },
                        },
                    },
                    CancellationToken.None);

                if (containers.Count > 0)
                {
                    await onLogLine($"[DEBUG] Related containers (project {run.ProjectId}):", LogStream.Stdout);
                    foreach (var c in containers.Take(5))
                    {
                        var names = string.Join(", ", c.Names.Select(n => n.TrimStart('/')));
                        await onLogLine($"[DEBUG]   {c.ID[..12]}  {names,-30}  state={c.State,-10}  status={c.Status}", LogStream.Stdout);
                    }
                }
            }
            catch { /* best-effort */ }
        }
        catch { /* diagnostics must never throw */ }
    }

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
            containerId, false, logsParams, cancellationToken);

        await DrainMultiplexedStreamAsync(stream, onLogLine, cancellationToken);
    }

    /// <summary>
    /// Executes a command inside a running container via Docker exec, streams its output through
    /// <paramref name="onLogLine"/>, and returns the process exit code.
    /// </summary>
    private async Task<long> ExecCommandAsync(
        string containerId,
        IList<string> cmd,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var execCreate = await dockerClient.Exec.ExecCreateContainerAsync(
            containerId,
            new ContainerExecCreateParameters
            {
                AttachStdout = true,
                AttachStderr = true,
                Cmd = cmd,
                WorkingDir = "/workspace",
            },
            cancellationToken);

        using var stream = await dockerClient.Exec.StartAndAttachContainerExecAsync(
            execCreate.ID, tty: false, cancellationToken);

        await DrainMultiplexedStreamAsync(stream, onLogLine, cancellationToken);

        var inspect = await dockerClient.Exec.InspectContainerExecAsync(execCreate.ID, CancellationToken.None);
        return inspect.ExitCode;
    }

    /// <summary>
    /// Executes a shell command (<c>/bin/sh -c <paramref name="script"/></c>) inside the container.
    /// Best-effort: errors are emitted as log lines but never propagated so they don't abort the run.
    /// </summary>
    private async Task ExecShellAsync(
        string containerId,
        string script,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        try
        {
            await ExecCommandAsync(containerId, ["/bin/sh", "-c", script], onLogLine, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await onLogLine($"[WARN] exec step error (non-fatal): {ex.Message}", LogStream.Stdout);
        }
    }

    /// <summary>
    /// Drains a <see cref="MultiplexedStream"/> (from container logs or exec attach), emitting each
    /// complete line through <paramref name="onLogLine"/>. Handles interleaved stdout/stderr correctly.
    /// </summary>
    private static async Task DrainMultiplexedStreamAsync(
        MultiplexedStream stream,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
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

    /// <summary>
    /// Builds a shell script that starts <c>dockerd</c> in the background and waits until its
    /// Unix socket is ready. Used for true DinD (the container runs with <c>Privileged=true</c>
    /// and manages its own isolated Docker daemon — the host socket is never mounted).
    /// Installs <c>docker.io</c> via apt if <c>dockerd</c> is not already present (fallback for
    /// older helper images that only shipped <c>docker-ce-cli</c>).
    /// When <paramref name="registryMirrorUrl"/> is provided, writes a <c>/etc/docker/daemon.json</c>
    /// that configures the daemon to use the specified URL as a pull-through registry mirror.
    /// </summary>
    internal static string BuildDindStartupScript(string? registryMirrorUrl = null)
    {
        // Start dockerd in the background, redirect its output, then poll the socket.
        // 'dockerd &' runs as PID 1's child; we give it up to 60 s to become healthy.
        // Use explicit \n to guarantee LF-only line endings when running inside a Linux container,
        // regardless of the line endings in this source file (e.g. CRLF on Windows).
        var lines = new List<string>
        {
            "command -v dockerd > /dev/null 2>&1 || (apt-get update -qq 2>/dev/null && apt-get install -y --no-install-recommends docker.io 2>/dev/null)",
        };

        if (!string.IsNullOrWhiteSpace(registryMirrorUrl))
        {
            // Write daemon.json before starting dockerd so the mirror is active from the first pull.
            // Use JsonSerializer to produce well-formed JSON, then single-quote it for the shell
            // (single quotes are safe here because the URL is validated to not contain single quotes).
            var daemonJson = JsonSerializer.Serialize(
                new { registryMirrors = new[] { registryMirrorUrl } },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower });
            lines.Add("mkdir -p /etc/docker");
            lines.Add($"printf '%s' '{daemonJson.Replace("'", "'\\''")}' > /etc/docker/daemon.json");
        }

        lines.AddRange([
            "dockerd > /tmp/dockerd.log 2>&1 &",
            "timeout=60",
            "while [ $timeout -gt 0 ] && ! docker info > /dev/null 2>&1; do",
            "  sleep 1; timeout=$((timeout-1))",
            "done",
            "docker info > /dev/null 2>&1 && echo '[DinD] dockerd ready' || { echo '[DinD] dockerd failed to start'; cat /tmp/dockerd.log; exit 1; }",
        ]);

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Ensures the pull-through registry mirror container (<c>issuepit-registry-mirror</c>) is running
    /// on the host. Creates and starts the container if it does not exist; restarts it if it is stopped.
    /// The container uses <c>registry:2</c> with <c>REGISTRY_PROXY_REMOTEURL</c> set to the Docker Hub
    /// upstream so it acts as a transparent pull-through cache.
    /// </summary>
    private async Task EnsureRegistryMirrorAsync(
        int port,
        string volumePath,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        // Check whether the mirror container already exists.
        ContainerInspectResponse? inspect = null;
        try
        {
            inspect = await dockerClient.Containers.InspectContainerAsync(RegistryMirrorContainerName, cancellationToken);
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Container does not exist — create it below.
        }

        if (inspect is not null)
        {
            if (inspect.State?.Running == true)
            {
                await onLogLine($"[DEBUG] Registry mirror: container '{RegistryMirrorContainerName}' already running", LogStream.Stdout);
                return;
            }

            // Exists but stopped — start it.
            await onLogLine($"[DEBUG] Registry mirror: starting existing container '{RegistryMirrorContainerName}'", LogStream.Stdout);
            await dockerClient.Containers.StartContainerAsync(RegistryMirrorContainerName, new ContainerStartParameters(), cancellationToken);
            return;
        }

        // Pull registry:2 if not present locally.
        await onLogLine($"[DEBUG] Registry mirror: pulling registry:2", LogStream.Stdout);
        await dockerClient.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = "registry", Tag = "2" },
            null,
            new Progress<JSONMessage>(),
            cancellationToken);

        // Create the pull-through mirror container.
        Directory.CreateDirectory(volumePath);
        await onLogLine($"[DEBUG] Registry mirror: creating container on port {port}", LogStream.Stdout);
        var createParams = new CreateContainerParameters
        {
            Image = "registry:2",
            Name = RegistryMirrorContainerName,
            Env = ["REGISTRY_PROXY_REMOTEURL=https://registry-1.docker.io"],
            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    // Bind to all interfaces so DinD containers can reach the registry via the
                    // Docker bridge gateway (host.docker.internal / 172.17.0.1). Binding to 127.0.0.1
                    // would not be reachable from the bridge network.
                    // Restrict external access at the OS firewall level if needed.
                    [$"{port}/tcp"] = [new PortBinding { HostIP = "0.0.0.0", HostPort = port.ToString() }],
                },
                Binds = [$"{volumePath}:/var/lib/registry"],
                RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped },
            },
            Labels = new Dictionary<string, string>
            {
                ["issuepit.component"] = "registry-mirror",
            },
        };

        await dockerClient.Containers.CreateContainerAsync(createParams, cancellationToken);
        await dockerClient.Containers.StartContainerAsync(RegistryMirrorContainerName, new ContainerStartParameters(), cancellationToken);
        await onLogLine($"[DEBUG] Registry mirror: started on 0.0.0.0:{port}", LogStream.Stdout);
    }
    /// first-run image-selection prompt. Uses <c>printf '%b'</c> so the leading <c>-P</c> in the actrc
    /// content is not misinterpreted as a printf option by <c>/bin/sh</c> (dash).
    /// </summary>
    private static string BuildActrcSetupScript(string actRunnerImage)
    {
        var platformLabels = new[] { "ubuntu-latest", "ubuntu-24.04", "ubuntu-22.04", "ubuntu-20.04" };
        // actrcBody: each line is "-P ubuntu-latest=<image>" joined with \n escape sequences.
        var actrcBody = string.Join("\\n", platformLabels.Select(label => $"-P {label}={actRunnerImage}"));

        // Use printf '%b' so the actrc content ('-P ubuntu-latest=...\n...') is passed as an argument,
        // not as the format string — preventing '/bin/sh: printf: Illegal option -P'.
        return $"mkdir -p /root/.config/act && printf '%b' '{actrcBody}\\n' > /root/.config/act/actrc";
    }

    /// <summary>
    /// Builds a shell script that runs <c>actionlint</c> on the workflow file when available.
    /// Tries two candidate locations inside the container workspace.
    /// Silently skipped when actionlint is not installed; <c>|| true</c> prevents aborting the run on lint errors.
    /// </summary>
    private static string BuildActionlintExecScript(string workflow)
    {
        var wfFileName = Path.GetFileName(workflow);
        var workflowRelPath = workflow.TrimStart('/').TrimStart('\\').Replace('\\', '/');
        var candidate1 = ShellQuote($"/workspace/.github/workflows/{wfFileName}");
        var candidate2 = ShellQuote($"/workspace/{workflowRelPath}");
        return
            // Resolve workflow file inside the container
            $"_wf=''; [ -f {candidate1} ] && _wf={candidate1} || [ -f {candidate2} ] && _wf={candidate2}; " +
            // Run actionlint when available and file was found; '|| true' keeps run alive on lint errors
            "command -v actionlint > /dev/null 2>&1 && [ -n \"$_wf\" ] && { echo '[ACTIONLINT] Validating workflow...'; actionlint -color=false \"$_wf\" 2>&1 || true; }";
    }

    /// <summary>
    /// Returns a POSIX single-quoted shell argument. Safe-quoting: wraps the argument in single quotes
    /// and escapes embedded single quotes as <c>'\''</c>.
    /// </summary>
    private static string ShellQuote(string arg)
    {
        // If the arg only contains safe characters, return as-is.
        if (SafeShellArgRegex().IsMatch(arg))
            return arg;
        return $"'{arg.Replace("'", "'\\''")}'";
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^[a-zA-Z0-9\-_./]+$")]
    private static partial System.Text.RegularExpressions.Regex SafeShellArgRegex();
}
