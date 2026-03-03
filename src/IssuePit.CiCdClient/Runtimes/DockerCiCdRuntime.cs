using System.Reflection;
using System.Text;
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

    private static string AppVersion =>
        Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

    // Builds a stable, human-readable Docker container name for a CI/CD run.
    private static string BuildContainerName(CiCdRun run) =>
        $"issuepit-cicd-{run.Id:N}"[..24]; // e.g. "issuepit-cicd-ab12cd34ef56"

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
        var actRunnerImage = configuration["CiCd__ActImage"] ?? "catthehacker/ubuntu:act-latest";

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
        if (!trigger.NoVolumeMounts && !hasGitRepo)
        {
            await onLogLine($"[DEBUG] Mount          : {workspacePath}:/workspace", LogStream.Stdout);
            if (!trigger.NoDind)
                await onLogLine($"[DEBUG] Mount          : /var/run/docker.sock:/var/run/docker.sock", LogStream.Stdout);
        }
        await onLogLine($"[DEBUG] Working dir    : /workspace", LogStream.Stdout);
        if (trigger.NoDind) await onLogLine($"[DEBUG] DinD           : disabled", LogStream.Stdout);
        if (trigger.NoVolumeMounts) await onLogLine($"[DEBUG] Volume mounts  : disabled", LogStream.Stdout);
        if (!string.IsNullOrWhiteSpace(trigger.CustomEntrypoint))
            await onLogLine($"[DEBUG] Entrypoint     : {trigger.CustomEntrypoint}", LogStream.Stdout);

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
        var binds = new List<string>();
        if (!trigger.NoVolumeMounts && !hasGitRepo)
        {
            binds.Add($"{workspacePath}:/workspace");
            if (!trigger.NoDind)
                // Mount Docker socket so act can spin up runner containers (DinD)
                binds.Add("/var/run/docker.sock:/var/run/docker.sock");
        }
        else if (hasGitRepo && !trigger.NoDind)
        {
            // Git-clone mode: still mount Docker socket for DinD even though workspace is cloned inside.
            binds.Add("/var/run/docker.sock:/var/run/docker.sock");
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
                var totalSteps = 2 + (hasGitRepo ? 1 : 0) + (!string.IsNullOrWhiteSpace(trigger.Workflow) ? 1 : 0);
                var stepNum = 0;

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
    /// rejected. The fix is to forcibly remove any existing container with the target name
    /// before each retry attempt.
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
                var reason = ex is DockerApiException ? "name conflict (container may have been created during a previous connection reset)" : "connection reset";
                await onLogLine(
                    $"[WARN] CreateContainer: {reason} (attempt {attempt}/{maxAttempts}), cleaning up and retrying in 2s…",
                    LogStream.Stderr);

                // Remove any existing container with the same name before retrying.
                // This handles the case where the daemon created the container but the response was lost.
                try
                {
                    await dockerClient.Containers.RemoveContainerAsync(
                        containerName,
                        new ContainerRemoveParameters { Force = true },
                        CancellationToken.None);
                }
                catch { /* best-effort: container may not exist */ }

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
    /// Builds a shell script that writes the <c>actrc</c> platform mapping to prevent the interactive
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
