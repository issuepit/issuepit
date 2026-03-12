using System.Reflection;
using System.Text;
using System.Text.Json;
using Docker.DotNet;
using Docker.DotNet.Models;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.Core.Services;

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
///   <item><c>CiCd__ActionCacheVolume</c> — named Docker volume for act action/repo cache
///     (default: <c>issuepit-action-cache</c>). Named volumes are managed by Docker and persist
///     independently of the <c>cicd-client</c> container lifecycle, which means they survive
///     container restarts without requiring any host-path bind mounts on the outer service.
///     To use a host path instead, set <c>CiCd__ActionCachePath</c> (or per-project/org setting).
///     To disable action caching entirely, set <c>CiCd__ActionCacheVolume</c> to an empty string.</item>
///   <item><c>CiCd__ActionCachePath</c> — explicit host path for the action cache. When set, takes
///     precedence over <c>CiCd__ActionCacheVolume</c>. Useful for development or bare-metal deployments
///     where the cicd-client process runs directly on the host.</item>
/// </list>
/// </summary>
public partial class DockerCiCdRuntime(
    ILogger<DockerCiCdRuntime> logger,
    DockerClient dockerClient,
    IConfiguration configuration) : ICiCdRuntime
{
    // Docker image used to run act. Uses the IssuePit helper-act image which includes
    // .NET SDK, Node.js, Playwright, Docker CLI, and act pre-installed.
    private const string DefaultImage = "ghcr.io/issuepit/issuepit-helper-act:latest";

    // Default DinD image cache settings.
    private const DindImageCacheStrategy DefaultDindCacheStrategy = DindImageCacheStrategy.RegistryMirror;
    private const string DefaultDindCacheVolumePath = "/var/lib/issuepit-dind-cache";
    private const string RegistryMirrorContainerName = "issuepit-registry-mirror";
    private const int DefaultRegistryMirrorPort = 5100;
    private const string DefaultRegistryMirrorVolumePath = "/var/lib/issuepit-registry-cache";

    // Default host path for the act action/repo cache. Used when neither the trigger nor
    // CiCd__ActionCachePath config key specifies a path. Mirrors the naming convention of
    // DefaultDindCacheVolumePath so operators can find and manage all IssuePit cache dirs
    // from the same parent directory.
    private const string DefaultActionCachePath = "/var/lib/issuepit-action-cache";

    // Default named Docker volume for the act action/repo cache.
    // Named volumes persist independently of the cicd-client container lifecycle, making them the
    // reliable default for containerised deployments where bind-mount host paths are not accessible.
    // This is used when no explicit host path (CiCd__ActionCachePath / trigger.ActionCachePath) is set.
    private const string DefaultActionCacheVolume = "issuepit-action-cache";

    // Default named Docker volume for the Playwright browser cache.
    // Playwright stores downloaded browser binaries here; job containers mount this volume at
    // /root/.cache/ms-playwright so browsers downloaded on one run are reused on subsequent runs.
    private const string DefaultPlaywrightCacheVolume = "issuepit-playwright-cache";

    // Fixed ports used by the apt-cacher-ng and HTTP cache (nginx) services.
    // These must match the port numbers configured in AppHost/docker-compose so that the iptables
    // DNAT rules written by BuildDindStartupScript can forward DinD job-container traffic to the
    // correct outer-host service without requiring dynamic port discovery.
    internal const int AptCachePort = 3142;
    internal const int HttpCachePort = 3143;

    // Docker bridge IP used by DinD job containers as their default gateway.
    // When InterceptAllTraffic is true, PLAYWRIGHT_DOWNLOAD_HOST is set to this IP + HttpCachePort
    // so job containers reach the http-cache nginx proxy via the iptables DNAT rules on the act container.
    // 172.17.0.1 is the standard default for the inner dockerd's bridge; override via config if needed.
    internal const string DefaultDindBridgeIp = "172.17.0.1";

    // Container-internal path where named package-cache volumes are mounted in the outer act
    // container. act passes these as --volume flags to each DinD job container it creates.
    private const string ContainerNpmCachePath = "/cache/npm";
    private const string ContainerNuGetCachePath = "/cache/nuget";
    private const string ContainerPlaywrightCachePath = "/cache/playwright";
    private const string ContainerActionCachePath = "/cache/actions";

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

        // Package cache: mount named volumes inside the act container so job containers can reuse
        // cached npm and NuGet packages across CI/CD runs (avoiding repeated network downloads).
        // The outer container exposes the cache directories; act passes them to each job container
        // via --volume so the inner dockerd (DinD) mounts them from the outer container's filesystem.
        var npmCacheVolume = configuration["CiCd__NpmCacheVolume"];
        var nugetCacheVolume = configuration["CiCd__NuGetCacheVolume"];
        var npmCacheUrl = configuration["CiCd__NpmCacheUrl"];

        // Apt and HTTP-cache (nginx) config.
        // - aptCacheUrl: URL of apt-cacher-ng. Port extracted for iptables DNAT rules.
        // - httpCacheUrl: URL of the http-cache nginx service. Port extracted for DNAT rules.
        // - playwrightCacheVolume: named volume for filesystem browser cache (always mounted when set).
        // - interceptAllTraffic: when true (and in DinD mode), enables iptables DNAT + apt proxy config
        //   so DinD job containers can reach the outer-host cache services through the Docker bridge.
        //   When false, only the filesystem volume cache is active (no network interception).
        var aptCacheUrl = configuration["CiCd__AptCacheUrl"];
        var httpCacheUrl = configuration["CiCd__HttpCacheUrl"];
        var playwrightCacheVolume = configuration["CiCd__PlaywrightCacheVolume"] ?? DefaultPlaywrightCacheVolume;
        var interceptAllTraffic = bool.TryParse(configuration["CiCd__InterceptAllTraffic"], out var iat) && iat;

        var useDind = !trigger.NoDind;

        if (!string.IsNullOrWhiteSpace(npmCacheUrl))
        {
            actBinAndArgs.Add("--env");
            actBinAndArgs.Add($"NPM_CONFIG_REGISTRY={npmCacheUrl}");
        }

        // When InterceptAllTraffic is enabled, iptables DNAT rules in BuildDindStartupScript
        // forward DinD job-container traffic destined for HttpCachePort to the outer http-cache
        // service. We set PLAYWRIGHT_DOWNLOAD_HOST to the default Docker bridge IP (172.17.0.1)
        // + HttpCachePort so Playwright in job containers reaches the http-cache proxy via the DNAT rule.
        // Without InterceptAllTraffic, DinD job containers cannot reach the host-side http-cache,
        // so we don't set PLAYWRIGHT_DOWNLOAD_HOST (Playwright falls back to cdn.playwright.dev
        // but will still use the filesystem browser cache mounted via the volume above).
        if (interceptAllTraffic && !string.IsNullOrWhiteSpace(httpCacheUrl) && useDind)
        {
            var dindBridgeIp = configuration["CiCd__DindBridgeIp"] ?? DefaultDindBridgeIp;
            actBinAndArgs.Add("--env");
            actBinAndArgs.Add($"PLAYWRIGHT_DOWNLOAD_HOST=http://{dindBridgeIp}:{HttpCachePort}");
        }

        // Pass volume mounts to act job containers via --container-options "-v src:dst".
        // act does not have a --volume flag; --container-options forwards raw Docker options to each
        // job container act creates (npm/NuGet/Playwright caches and the apt proxy config file).
        var containerOptions = BuildActContainerOptions(
            npmCacheVolume, nugetCacheVolume, playwrightCacheVolume,
            interceptAllTraffic && !string.IsNullOrWhiteSpace(aptCacheUrl) && useDind);
        if (containerOptions is not null)
        {
            actBinAndArgs.Add("--container-options");
            actBinAndArgs.Add(containerOptions);
        }

        // Action cache: resolve the effective cache mount.
        //
        // Priority (host path):   trigger.ActionCachePath → CiCd__ActionCachePath config
        // Priority (named volume): CiCd__ActionCacheVolume config → DefaultActionCacheVolume ("issuepit-action-cache")
        //
        // A named Docker volume is used by default when no explicit host path is configured.
        // Named volumes are managed by the Docker daemon and persist across cicd-client container
        // restarts without any host-side volume mount, making them the correct choice for
        // containerised deployments (Docker Compose, Kubernetes, etc.).
        //
        // If an explicit host path is set (trigger or config), it takes full precedence and the
        // named-volume fallback is skipped.  The host path is replaced with ContainerActionCachePath
        // inside the act command so act always writes to /cache/actions regardless of which
        // mechanism supplies the mount.
        //
        // Disable caching entirely by setting CiCd__ActionCacheVolume="" and leaving ActionCachePath
        // unset (or by setting CiCd__ActionCachePath to an empty string and ActionCachePath to null).
        var actionCacheHostPath = !string.IsNullOrWhiteSpace(trigger.ActionCachePath)
            ? trigger.ActionCachePath
            : configuration["CiCd__ActionCachePath"]; // explicit host-path override only

        // Resolve the named-volume fallback when no host path is configured.
        var actionCacheVolumeName = string.IsNullOrWhiteSpace(actionCacheHostPath)
            ? (configuration["CiCd__ActionCacheVolume"] ?? DefaultActionCacheVolume)
            : null;

        if (!string.IsNullOrWhiteSpace(actionCacheHostPath))
        {
            // Host path: replace the arg BuildActArgumentsList inserted with the container-internal path.
            var idx = actBinAndArgs.IndexOf(actionCacheHostPath);
            if (idx >= 0)
                actBinAndArgs[idx] = ContainerActionCachePath;
            else
            {
                // ActionCachePath came from config (not from trigger field) — the arg wasn't added yet.
                actBinAndArgs.Add("--action-cache-path");
                actBinAndArgs.Add(ContainerActionCachePath);
            }
        }
        else if (!string.IsNullOrWhiteSpace(actionCacheVolumeName))
        {
            // Named volume: trigger.ActionCachePath was null so BuildActArgumentsList didn't add the flag.
            actBinAndArgs.Add("--action-cache-path");
            actBinAndArgs.Add(ContainerActionCachePath);
        }

        // Append a short suffix derived from the run ID so act job containers inside the DinD
        // daemon are identifiable by run (issuepit/act --container-name-suffix).
        var actContainerNameSuffix = "-" + $"{run.Id:N}"[..NativeCiCdRuntime.ContainerNameSuffixLength];
        actBinAndArgs.Add("--container-name-suffix");
        actBinAndArgs.Add(actContainerNameSuffix);

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
        if (!string.IsNullOrWhiteSpace(npmCacheVolume))
            await onLogLine($"[DEBUG] npm cache vol  : {npmCacheVolume}:{ContainerNpmCachePath} (outer container mount, passed to job containers via --container-options)", LogStream.Stdout);
        if (!string.IsNullOrWhiteSpace(npmCacheUrl))
            await onLogLine($"[DEBUG] npm registry   : {npmCacheUrl}", LogStream.Stdout);
        if (!string.IsNullOrWhiteSpace(nugetCacheVolume))
            await onLogLine($"[DEBUG] NuGet cache vol: {nugetCacheVolume}:{ContainerNuGetCachePath} (outer container mount, passed to job containers via --container-options)", LogStream.Stdout);
        if (!string.IsNullOrWhiteSpace(playwrightCacheVolume))
            await onLogLine($"[DEBUG] Playwright vol : {playwrightCacheVolume}:{ContainerPlaywrightCachePath} → /root/.cache/ms-playwright (filesystem browser cache)", LogStream.Stdout);
        if (!string.IsNullOrWhiteSpace(httpCacheUrl))
            await onLogLine($"[DEBUG] HTTP cache URL : {httpCacheUrl} (nginx; playwright CDN + GitHub releases)", LogStream.Stdout);
        if (!string.IsNullOrWhiteSpace(aptCacheUrl))
            await onLogLine($"[DEBUG] Apt cache URL  : {aptCacheUrl} (apt-cacher-ng; stats at /acng-report.html)", LogStream.Stdout);
        if (interceptAllTraffic)
            await onLogLine($"[DEBUG] Traffic intercept: enabled (iptables DNAT + apt proxy config in DinD job containers)", LogStream.Stdout);
        else
            await onLogLine($"[DEBUG] Traffic intercept: disabled (set CiCd__InterceptAllTraffic=true to enable apt proxy + PLAYWRIGHT_DOWNLOAD_HOST)", LogStream.Stdout);
        if (!string.IsNullOrWhiteSpace(actionCacheHostPath))
            await onLogLine($"[DEBUG] Action cache   : {actionCacheHostPath}:{ContainerActionCachePath} (host bind-mount)", LogStream.Stdout);
        else if (!string.IsNullOrWhiteSpace(actionCacheVolumeName))
            await onLogLine($"[DEBUG] Action cache   : {actionCacheVolumeName}:{ContainerActionCachePath} (named Docker volume)", LogStream.Stdout);
        if (trigger.UseNewActionCache == true)
            await onLogLine($"[DEBUG] New action cache: enabled (--use-new-action-cache)", LogStream.Stdout);
        if (trigger.ActionOfflineMode == true)
            await onLogLine($"[DEBUG] Offline mode   : enabled (--action-offline-mode)", LogStream.Stdout);
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
                null!,
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

        // Artifacts are no longer volume-mounted. Instead, after the run the container uploads
        // the /artifacts directory directly to S3 using the AWS CLI. This removes the requirement
        // for a shared filesystem path between the cicd-client process and the Docker host, enabling
        // fully containerised deployments (Docker Compose, Kubernetes, etc.) where bind-mounting a
        // path from inside the cicd-client container is not possible.

        // Mount named Docker volumes for package caches so their contents persist across CI/CD runs.
        // The paths inside the container match what act's job containers (DinD) will bind-mount
        // via the --volume flags already appended to actBinAndArgs above.
        if (!string.IsNullOrWhiteSpace(npmCacheVolume))
            binds.Add($"{npmCacheVolume}:{ContainerNpmCachePath}");
        if (!string.IsNullOrWhiteSpace(nugetCacheVolume))
            binds.Add($"{nugetCacheVolume}:{ContainerNuGetCachePath}");
        if (!string.IsNullOrWhiteSpace(playwrightCacheVolume))
            binds.Add($"{playwrightCacheVolume}:{ContainerPlaywrightCachePath}");

        // Mount action/repo cache so act can reuse previously cloned actions across runs.
        // Named Docker volumes (default) persist independently of the cicd-client container lifecycle,
        // which is the correct choice when cicd-client itself runs inside a Docker container (e.g.,
        // docker-compose / Kubernetes) — the volume is managed by the Docker daemon on the host,
        // not by any filesystem path inside cicd-client.
        // An explicit host path (actionCacheHostPath) overrides the named volume and behaves the
        // same way as the DinD cache and artifact mounts (bind-mount to a real path on the machine
        // running the Docker daemon — suitable for bare-metal / development setups).
        if (!string.IsNullOrWhiteSpace(actionCacheHostPath))
        {
            Directory.CreateDirectory(actionCacheHostPath);
            binds.Add($"{actionCacheHostPath}:{ContainerActionCachePath}");
        }
        else if (!string.IsNullOrWhiteSpace(actionCacheVolumeName))
        {
            // Named volume: Docker auto-creates it on first use; no Directory.CreateDirectory needed.
            binds.Add($"{actionCacheVolumeName}:{ContainerActionCachePath}");
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

            await EnsureRegistryMirrorAsync(mirrorPort, mirrorVolumePath, onLogLine, cancellationToken);
            // Use host.docker.internal so the DinD dockerd inside the container can reach the registry
            // on the host. Docker 20.10+ resolves this to the host gateway IP on Linux.
            registryMirrorUrl = $"http://host.docker.internal:{mirrorPort}";
            extraHosts.Add("host.docker.internal:host-gateway");
            await onLogLine($"[DEBUG] Registry mirror: {registryMirrorUrl}", LogStream.Stdout);
            // Note: to fall back to LocalVolume cache when the mirror is unavailable, wrap the
            // EnsureRegistryMirrorAsync call and the three lines that follow it in a try/catch,
            // set registryMirrorUrl = null in the catch block, and clear the extraHosts entry.
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
                var hasArtifacts = !string.IsNullOrWhiteSpace(trigger.ArtifactServerPath);
                var hasS3 = !string.IsNullOrWhiteSpace(configuration["ImageStorage:ServiceUrl"]);
                var totalSteps = 3 + (useDind ? 1 : 0) + (hasGitRepo ? 1 : 0) + (!string.IsNullOrWhiteSpace(trigger.Workflow) ? 1 : 0) + (hasArtifacts && hasS3 ? 1 : 0);
                var stepNum = 0;

                // Step: Print act version for diagnostics.
                await onLogLine($"[DEBUG] Step {++stepNum}/{totalSteps}: act --version", LogStream.Stdout);
                var capturedVersion = "";
                await ExecShellAsync(
                    container.ID,
                    "act --version 2>&1 || true",
                    (line, _) => { if (!string.IsNullOrWhiteSpace(line)) capturedVersion = line.Trim(); return Task.CompletedTask; },
                    cancellationToken);
                await onLogLine($"[DEBUG] Act version    : {(string.IsNullOrEmpty(capturedVersion) ? "unknown" : capturedVersion)}", LogStream.Stdout);

                // Step: Start dockerd (true DinD — no host socket mount).
                // The first exec step starts the in-container daemon and waits until it is ready.
                if (useDind)
                {
                    await onLogLine($"[DEBUG] Step {++stepNum}/{totalSteps}: starting dockerd (DinD)", LogStream.Stdout);
                    // Pass cache ports only when InterceptAllTraffic is enabled so that iptables DNAT
                    // rules and the apt proxy config are only set up when the operator has opted in.
                    var aptCachePort = interceptAllTraffic ? ParsePort(aptCacheUrl) : null;
                    var httpCachePort = interceptAllTraffic ? ParsePort(httpCacheUrl) : null;
                    var dindExitCode = await ExecCommandAsync(
                        container.ID,
                        ["/bin/sh", "-c", BuildDindStartupScript(registryMirrorUrl, aptCachePort, httpCachePort)],
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

                    // Parse the workflow graph from the cloned repo by cat-ing the YAML files
                    // inside the container. Sets run.WorkflowGraphJson so the worker can persist it.
                    await ParseWorkflowGraphFromContainerAsync(container.ID, run, onLogLine, cancellationToken);

                    // Best-effort: run actionlint on all workflow files in the container after clone.
                    // Output is streamed to the run log. Never aborts the run.
                    await ExecShellAsync(
                        container.ID,
                        "command -v actionlint > /dev/null 2>&1 && [ -d /workspace/.github/workflows ] && " +
                        "{ echo '[ACTIONLINT] Validating workflows...'; actionlint -color=false /workspace/.github/workflows/*.yml 2>&1 || true; }",
                        onLogLine,
                        cancellationToken);
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

                // Step: Upload artifacts to S3 from inside the container (best-effort, regardless of act exit code).
                // This replaces the previous volume-mount approach: the container pushes artifacts
                // directly so no shared filesystem path is required between the cicd-client and the host.
                if (hasArtifacts && hasS3)
                {
                    await onLogLine($"[DEBUG] Step {++stepNum}/{totalSteps}: uploading artifacts to S3", LogStream.Stdout);
                    await UploadArtifactsToS3Async(container.ID, run.Id, onLogLine, cancellationToken);
                }

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
            containerId, logsParams, cancellationToken);

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
        CancellationToken cancellationToken,
        IList<string>? env = null)
    {
        var execCreate = await dockerClient.Exec.CreateContainerExecAsync(
            containerId,
            new ContainerExecCreateParameters
            {
                AttachStdout = true,
                AttachStderr = true,
                Cmd = cmd,
                WorkingDir = "/workspace",
                Env = env,
            },
            cancellationToken);

        using var stream = await dockerClient.Exec.StartContainerExecAsync(
            execCreate.ID, new ContainerExecStartParameters { Detach = false }, cancellationToken);

        await DrainMultiplexedStreamAsync(stream, onLogLine, cancellationToken);

        var inspect = await dockerClient.Exec.InspectContainerExecAsync(execCreate.ID, CancellationToken.None);
        return inspect.ExitCode ?? 0;
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
    /// Executes a command inside the container and returns the stdout output as a string.
    /// Stderr is discarded. Never throws on non-zero exit codes.
    /// </summary>
    private async Task<string> ExecCommandCaptureAsync(
        string containerId,
        IList<string> cmd,
        CancellationToken cancellationToken)
    {
        var execCreate = await dockerClient.Exec.CreateContainerExecAsync(
            containerId,
            new ContainerExecCreateParameters
            {
                AttachStdout = true,
                AttachStderr = false,
                Cmd = cmd,
                WorkingDir = "/workspace",
            },
            cancellationToken);

        using var stream = await dockerClient.Exec.StartContainerExecAsync(
            execCreate.ID, new ContainerExecStartParameters { Detach = false }, cancellationToken);

        var sb = new StringBuilder();
        await DrainMultiplexedStreamAsync(
            stream,
            (line, _) => { sb.AppendLine(line); return Task.CompletedTask; },
            cancellationToken);

        return sb.ToString();
    }

    /// <summary>
    /// Uploads the contents of <c>/artifacts</c> inside the container to S3-compatible storage
    /// using the AWS CLI, replacing the previous volume-mount approach.
    ///
    /// The method is best-effort: a failed upload is logged as a warning but never aborts the run.
    ///
    /// If the AWS CLI is not present in the container it is installed automatically
    /// (pip3 install awscli → apt-get install awscli fallback).
    ///
    /// Credentials and endpoint are injected as exec-scoped environment variables so they are
    /// never visible in the container's environment or in act's job containers.
    /// </summary>
    private async Task UploadArtifactsToS3Async(
        string containerId,
        Guid runId,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var serviceUrl = configuration["ImageStorage:ServiceUrl"];
        if (string.IsNullOrWhiteSpace(serviceUrl))
            return;

        var accessKey = configuration["ImageStorage:AccessKey"] ?? "test";
        var secretKey = configuration["ImageStorage:SecretKey"] ?? "test";
        var region = configuration["ImageStorage:Region"] ?? "us-east-1";
        var bucket = configuration["ImageStorage:BucketName"] ?? "issuepit-uploads";
        var runIdHex = runId.ToString("N");

        var env = new List<string>
        {
            $"AWS_ACCESS_KEY_ID={accessKey}",
            $"AWS_SECRET_ACCESS_KEY={secretKey}",
            $"AWS_DEFAULT_REGION={region}",
            $"ISSUEPIT_S3_BUCKET={bucket}",
            $"ISSUEPIT_S3_ENDPOINT_URL={serviceUrl}",
            $"ISSUEPIT_RUN_ID={runIdHex}",
        };

        var script = BuildArtifactUploadScript();

        try
        {
            var exitCode = await ExecCommandAsync(
                containerId,
                ["/bin/sh", "-c", script],
                onLogLine,
                cancellationToken,
                env);

            if (exitCode != 0)
                await onLogLine($"[WARN] Artifact S3 upload exited with code {exitCode} — artifacts may not be available for download", LogStream.Stdout);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await onLogLine($"[WARN] Artifact S3 upload failed (non-fatal): {ex.Message}", LogStream.Stdout);
        }
    }

    /// <summary>
    /// Builds the shell script that checks for s5cmd, installs it if missing, and then
    /// syncs <c>/artifacts</c> to <c>s3://{bucket}/artifacts-raw/{runId}/</c>.
    /// Uses <c>s5cmd</c> (FOSS, MIT-licensed) instead of the AWS CLI.
    /// Environment variables expected: <c>AWS_ACCESS_KEY_ID</c>, <c>AWS_SECRET_ACCESS_KEY</c>,
    /// <c>AWS_DEFAULT_REGION</c>, <c>ISSUEPIT_S3_BUCKET</c>, <c>ISSUEPIT_S3_ENDPOINT_URL</c>,
    /// <c>ISSUEPIT_RUN_ID</c>.
    /// </summary>
    private static string BuildArtifactUploadScript() => """
        set -e
        # Exit early if the artifacts directory is empty.
        if [ ! -d /artifacts ] || [ -z "$(ls -A /artifacts 2>/dev/null)" ]; then
            echo '[S3-UPLOAD] No artifacts to upload'
            exit 0
        fi
        # The IssuePit helper-base image ships s5cmd pre-installed.
        # For non-standard images that do not include s5cmd, attempt a runtime
        # installation as a safety net before running the upload.
        if ! command -v s5cmd > /dev/null 2>&1; then
            echo '[S3-UPLOAD] s5cmd not found, installing...'
            ARCH=$(uname -m)
            case "$ARCH" in
              x86_64)  S5CMD_ARCH=Linux-64bit ;;
              aarch64) S5CMD_ARCH=Linux-arm64 ;;
              *) echo "[S3-UPLOAD] Unsupported architecture: $ARCH"; exit 1 ;;
            esac
            curl --proto '=https' --tlsv1.2 -fsSL \
                "https://github.com/peak/s5cmd/releases/download/v2.3.0/s5cmd_2.3.0_${S5CMD_ARCH}.tar.gz" | \
            tar -xz -C /usr/local/bin s5cmd
        fi
        echo "[S3-UPLOAD] Uploading artifacts to s3://${ISSUEPIT_S3_BUCKET}/artifacts-raw/${ISSUEPIT_RUN_ID}/"
        s5cmd --endpoint-url "${ISSUEPIT_S3_ENDPOINT_URL}" \
            sync /artifacts/ "s3://${ISSUEPIT_S3_BUCKET}/artifacts-raw/${ISSUEPIT_RUN_ID}/"
        echo '[S3-UPLOAD] Upload complete'
        """;

    /// <summary>
    /// Reads the workflow YAML files from the cloned repo inside the container using <c>cat</c>,
    /// parses the job graph with <see cref="WorkflowGraphParser.ParseFromStringsAsync"/>,
    /// and sets <see cref="CiCdRun.WorkflowGraphJson"/> on the run object so the worker can
    /// persist it after <c>RunAsync</c> returns.
    /// Best-effort: errors are emitted as debug log lines but never abort the run.
    /// </summary>
    private async Task ParseWorkflowGraphFromContainerAsync(
        string containerId,
        CiCdRun run,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        try
        {
            // List workflow files inside the container.
            var fileListRaw = await ExecCommandCaptureAsync(
                containerId,
                ["/bin/sh", "-c", "find /workspace/.github/workflows -maxdepth 1 \\( -name '*.yml' -o -name '*.yaml' \\) 2>/dev/null"],
                cancellationToken);

            var files = fileListRaw
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .Where(f => !string.IsNullOrEmpty(f))
                .ToList();

            if (files.Count == 0)
                return;

            // Cat each file and collect content keyed by the base filename.
            var fileContents = new Dictionary<string, string>();
            foreach (var filePath in files)
            {
                var content = await ExecCommandCaptureAsync(
                    containerId,
                    ["/bin/sh", "-c", $"cat {ShellQuote(filePath)}"],
                    cancellationToken);
                if (!string.IsNullOrWhiteSpace(content))
                    fileContents[Path.GetFileName(filePath)] = content;
            }

            if (fileContents.Count == 0)
                return;

            // Parse using the string-based multi-file API (uses ParseFromStringAsync internally).
            var graph = await WorkflowGraphParser.ParseFromStringsAsync(fileContents, cancellationToken);
            run.WorkflowGraphJson = JsonSerializer.Serialize(graph);
            await onLogLine($"[DEBUG] Workflow graph parsed from cloned repo ({fileContents.Count} file(s), {graph.Jobs.Count} job(s))", LogStream.Stdout);
        }
        catch (Exception ex)
        {
            await onLogLine($"[DEBUG] Could not parse workflow graph from container: {ex.Message}", LogStream.Stdout);
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
    /// Builds the <c>--container-options</c> value that is passed to <c>act</c> so it forwards
    /// the specified volume mounts to every job container it creates.
    /// act does not have a <c>--volume</c> flag; all volume mounts must be expressed as
    /// Docker <c>-v</c> flags inside a single <c>--container-options</c> string.
    /// Returns <c>null</c> when no volumes need to be mounted.
    /// </summary>
    /// <param name="npmCacheVolume">Named volume or host path for the npm package cache (mounted at /cache/npm).</param>
    /// <param name="nugetCacheVolume">Named volume or host path for the NuGet package cache (mounted at /cache/nuget).</param>
    /// <param name="playwrightCacheVolume">Named volume or host path for the Playwright browser cache (mounted at /cache/playwright).</param>
    /// <param name="includeAptProxy">
    ///   When <c>true</c>, also mounts the apt proxy config file
    ///   (<c>/etc/apt/apt.conf.d/01proxy</c>) that <see cref="BuildDindStartupScript"/> writes
    ///   on the act container so every job container picks up the transparent apt proxy.
    /// </param>
    internal static string? BuildActContainerOptions(
        string? npmCacheVolume,
        string? nugetCacheVolume,
        string? playwrightCacheVolume,
        bool includeAptProxy = false)
    {
        var opts = new List<string>();
        if (!string.IsNullOrWhiteSpace(npmCacheVolume))
            opts.Add($"-v {ContainerNpmCachePath}:/root/.npm");
        if (!string.IsNullOrWhiteSpace(nugetCacheVolume))
            opts.Add($"-v {ContainerNuGetCachePath}:/root/.nuget/packages");
        if (!string.IsNullOrWhiteSpace(playwrightCacheVolume))
            opts.Add($"-v {ContainerPlaywrightCachePath}:/root/.cache/ms-playwright");
        if (includeAptProxy)
            opts.Add("-v /etc/apt/apt.conf.d/01proxy:/etc/apt/apt.conf.d/01proxy");
        return opts.Count > 0 ? string.Join(" ", opts) : null;
    }

    /// <summary>
    /// Builds a shell script that starts <c>dockerd</c> in the background and waits until its
    /// Unix socket is ready. Used for true DinD (the container runs with <c>Privileged=true</c>
    /// and manages its own isolated Docker daemon — the host socket is never mounted).
    /// Installs <c>docker.io</c> via apt if <c>dockerd</c> is not already present (fallback for
    /// older helper images that only shipped <c>docker-ce-cli</c>).
    /// When <paramref name="registryMirrorUrl"/> is provided, writes a <c>/etc/docker/daemon.json</c>
    /// that configures the daemon to use the specified URL as a pull-through registry mirror.
    /// When <paramref name="aptCachePort"/> is provided, sets up iptables DNAT rules so that DinD
    /// job containers can reach the apt-cacher-ng service on the outer Docker host, and writes
    /// <c>/etc/apt/apt.conf.d/01proxy</c> on the act container so act can volume-mount it into
    /// each job container (transparent apt proxy).
    /// When <paramref name="httpCachePort"/> is provided, sets up iptables DNAT rules so that
    /// DinD job containers can reach the HTTP cache (nginx reverse proxy) on the outer host.
    /// </summary>
    internal static string BuildDindStartupScript(
        string? registryMirrorUrl = null,
        int? aptCachePort = null,
        int? httpCachePort = null)
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

        // ── iptables DNAT: forward cache-service ports from DinD bridge to outer Docker host ─────────
        //
        // DinD job containers use the act container's inner docker0 bridge as their gateway
        // (172.17.x.1). By adding PREROUTING DNAT rules on the act container (which runs with
        // Privileged=true), traffic destined for those ports is forwarded to the outer Docker
        // host where the real cache services (apt-cacher-ng, http-cache nginx) are running.
        //
        // host.docker.internal is resolvable inside the act container via its ExtraHosts entry
        // (added by DockerCiCdRuntime when creating the container).
        if (aptCachePort.HasValue || httpCachePort.HasValue)
        {
            lines.Add("# Set up iptables DNAT so DinD job containers can reach outer-host cache services.");
            lines.Add("OUTER_IP=$(getent hosts host.docker.internal 2>/dev/null | awk '{print $1}')");
            lines.Add("if [ -n \"$OUTER_IP\" ]; then");
            lines.Add("  echo 1 > /proc/sys/net/ipv4/ip_forward");
            if (aptCachePort.HasValue)
                lines.Add($"  iptables -t nat -A PREROUTING -p tcp --dport {aptCachePort.Value} -j DNAT --to-destination \"${{OUTER_IP}}:{aptCachePort.Value}\" 2>/dev/null || true");
            if (httpCachePort.HasValue)
                lines.Add($"  iptables -t nat -A PREROUTING -p tcp --dport {httpCachePort.Value} -j DNAT --to-destination \"${{OUTER_IP}}:{httpCachePort.Value}\" 2>/dev/null || true");
            lines.Add("  iptables -t nat -A POSTROUTING -j MASQUERADE 2>/dev/null || true");
            lines.Add("fi");
        }

        lines.AddRange([
            "dockerd > /tmp/dockerd.log 2>&1 &",
            "timeout=60",
            "while [ $timeout -gt 0 ] && ! docker info > /dev/null 2>&1; do",
            "  sleep 1; timeout=$((timeout-1))",
            "done",
            "docker info > /dev/null 2>&1 && echo '[DinD] dockerd ready' || { echo '[DinD] dockerd failed to start'; cat /tmp/dockerd.log; exit 1; }",
        ]);

        // ── apt proxy config ──────────────────────────────────────────────────────────────────────────
        //
        // After dockerd is ready we can determine the inner Docker bridge IP (docker0 interface).
        // Job containers reach this IP as their gateway; iptables DNAT above forwards the traffic
        // to the outer apt-cacher-ng. Write Acquire::http::Proxy so apt-get in job containers uses
        // the proxy transparently (the act --volume flag mounts this file into each job container).
        if (aptCachePort.HasValue)
        {
            lines.Add("# Write apt proxy config for DinD job containers.");
            lines.Add("DOCKER_BRIDGE_IP=$(ip addr show docker0 2>/dev/null | awk '/inet / {split($2,a,\"/\"); print a[1]}')");
            lines.Add("if [ -n \"$DOCKER_BRIDGE_IP\" ]; then");
            lines.Add("  mkdir -p /etc/apt/apt.conf.d");
            lines.Add($"  printf 'Acquire::http::Proxy \"http://%s:{aptCachePort.Value}\";\\n' \"$DOCKER_BRIDGE_IP\" > /etc/apt/apt.conf.d/01proxy");
            lines.Add($"  echo \"[DinD] Apt proxy: http://${{DOCKER_BRIDGE_IP}}:{aptCachePort.Value} (DNAT → ${{OUTER_IP:-outer-host}}:{aptCachePort.Value})\"");
            lines.Add("fi");
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Extracts the port number from a URL string. Returns <c>null</c> if the URL is empty,
    /// malformed, or does not contain an explicit port.
    /// </summary>
    private static int? ParsePort(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Port != -1)
            return uri.Port;
        return null;
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
            null!,
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
