using System.Reflection;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.Core.Runners;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>Runs the agent inside a local Docker container with Docker-in-Docker (DinD) support.</summary>
public class DockerAgentRuntime(ILogger<DockerAgentRuntime> logger, DockerClient dockerClient, IConfiguration configuration) : IAgentRuntime
{
    // Docker image used to run agents. Uses the IssuePit helper-opencode-act image which includes
    // the opencode CLI, Docker Engine (DinD), act, .NET SDK, Node.js, and Playwright.
    // Overridden by agent.DockerImage when set.
    private const string DefaultDockerImage = "ghcr.io/issuepit/issuepit-helper-opencode-act:main-dotnet10-node24";

    /// <summary>Read buffer size for streaming container log output. 80 KiB matches the Docker SDK convention.</summary>
    private const int LogBufferSize = 81920;

    private static string AppVersion =>
        Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

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
        {
            await onLogLine($"[DEBUG] Git remote     : {gitRepository.RemoteUrl}", LogStream.Stdout);
            // Determine the branch the container will check out: issue.GitBranch takes precedence
            // (feature branch for this issue), otherwise falls back to the repo's default branch.
            var effectiveBranch = !string.IsNullOrWhiteSpace(issue.GitBranch)
                ? issue.GitBranch
                : gitRepository.DefaultBranch;
            if (!string.IsNullOrWhiteSpace(effectiveBranch))
                await onLogLine($"[DEBUG] Git branch     : {effectiveBranch}", LogStream.Stdout);
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
        var env = AgentEnvironmentBuilder.Build(session, agent, issue, credentials, gitRepository);

        // Step 3: Build runner-specific CMD args to override the container's default entrypoint args.
        // Tool setup (npm install, dotnet restore, etc.) is handled by the container's entrypoint script.
        var runnerArgs = RunnerCommandBuilder.BuildArgsList(agent, issue);
        var cmd = runnerArgs.Count > 0 ? runnerArgs.ToList() : null;

        // Step 4: Configure DNS-based firewall when internet access should be restricted.
        // The restricted DNS server blocks general internet while keeping development domains reachable.
        var dns = agent.DisableInternet ? GetRestrictedDns() : null;

        logger.LogInformation("Creating Docker container from image {Image} for agent {AgentId} (DisableInternet={DisableInternet})",
            image, agent.Id, agent.DisableInternet);

        var hostConfig = new HostConfig
        {
            // Privileged mode is required for true DinD (in-container dockerd).
            // The host Docker socket is never mounted — agent tools run inside the container's
            // own isolated Docker daemon, fully isolated from the host.
            Privileged = true,
            // Keep the container after exit when KeepContainer is set so developers can inspect
            // the container filesystem or re-attach for debugging. Default is to auto-remove.
            AutoRemove = !session.KeepContainer,
        };

        if (dns is not null)
            hostConfig.DNS = dns;

        var createParams = new CreateContainerParameters
        {
            Image = image,
            Env = env,
            Cmd = cmd,
            HostConfig = hostConfig,
            Labels = new Dictionary<string, string>
            {
                ["issuepit.session-id"] = session.Id.ToString(),
                ["issuepit.issue-id"] = issue.Id.ToString(),
                ["issuepit.agent-id"] = agent.Id.ToString(),
            },
        };

        // Step 5: Create and start the container to execute the CLI agent tool.
        var container = await dockerClient.Containers.CreateContainerAsync(
            createParams, cancellationToken);

        await dockerClient.Containers.StartContainerAsync(
            container.ID, new ContainerStartParameters(), cancellationToken);

        var shortContainerId = container.ID[..Math.Min(12, container.ID.Length)];
        await onLogLine($"[DEBUG] Container ID   : {shortContainerId}", LogStream.Stdout);

        logger.LogInformation("Started Docker container {ContainerId} for agent session {SessionId}",
            container.ID, session.Id);

        // Step 6: Stream container logs until the container exits and capture exit code.
        // WaitContainerAsync runs concurrently with log streaming so we can get the
        // container exit code even when AutoRemove=true (the container is already removed
        // by the time StreamContainerLogsAsync returns).
        var logStreamTask = StreamContainerLogsAsync(container.ID, onLogLine, cancellationToken);
        var waitResponse = await dockerClient.Containers.WaitContainerAsync(container.ID, cancellationToken);
        await logStreamTask;

        if (waitResponse.StatusCode != 0)
            throw new Exception(
                $"Agent container exited with code {waitResponse.StatusCode} " +
                $"(image: {image}, session: {session.Id})");

        return container.ID;
    }

    /// <summary>
    /// Streams the container's stdout/stderr to <paramref name="onLogLine"/> and blocks until the container exits.
    /// Uses <c>Follow=true</c> so the call does not return until the container stops.
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

    /// <summary>Explicitly pulls the Docker image, ensuring the latest version is available before container creation.</summary>
    private async Task PullImageAsync(string image, CancellationToken cancellationToken)
    {
        logger.LogInformation("Pulling Docker image {Image}", image);

        // Parse image reference into name and tag.
        // Handle registry:port/name:tag format by finding the colon that appears after the last slash.
        // e.g. "localhost:5000/myimage:v1" → fromImage="localhost:5000/myimage", tag="v1"
        // e.g. "ghcr.io/org/image:tag"    → fromImage="ghcr.io/org/image", tag="tag"
        // e.g. "image:tag"                → fromImage="image", tag="tag"
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
}
