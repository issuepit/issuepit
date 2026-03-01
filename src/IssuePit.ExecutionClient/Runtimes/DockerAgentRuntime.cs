using Docker.DotNet;
using Docker.DotNet.Models;
using IssuePit.Core.Entities;
using IssuePit.Core.Runners;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>Runs the agent inside a local Docker container with Docker-in-Docker (DinD) support.</summary>
public class DockerAgentRuntime(ILogger<DockerAgentRuntime> logger, DockerClient dockerClient, IConfiguration configuration) : IAgentRuntime
{
    public async Task<string> LaunchAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        RuntimeConfiguration? runtimeConfig,
        GitRepository? gitRepository,
        CancellationToken cancellationToken)
    {
        // Step 1: Pull the container image explicitly before creating the container.
        await PullImageAsync(agent.DockerImage, cancellationToken);

        // Step 2: Build environment including git repo info so the container can clone the repo on startup.
        var env = BuildEnvironment(session, agent, issue, credentials, gitRepository);

        // Step 3: Build runner-specific CMD args to override the container's default entrypoint args.
        // Tool setup (npm install, dotnet restore, etc.) is handled by the container's entrypoint script.
        var runnerArgs = RunnerCommandBuilder.BuildArgsList(agent, issue);
        var cmd = runnerArgs.Count > 0 ? runnerArgs.ToList() : null;

        // Step 4: Configure DNS-based firewall when internet access should be restricted.
        // The restricted DNS server blocks general internet while keeping development domains reachable.
        var dns = agent.DisableInternet ? GetRestrictedDns() : null;

        logger.LogInformation("Creating Docker container from image {Image} for agent {AgentId} (DisableInternet={DisableInternet})",
            agent.DockerImage, agent.Id, agent.DisableInternet);

        var hostConfig = new HostConfig
        {
            // Mount Docker socket for Docker-in-Docker (DinD) support
            Binds = ["/var/run/docker.sock:/var/run/docker.sock"],
            AutoRemove = true,
        };

        if (dns is not null)
            hostConfig.DNS = dns;

        var createParams = new CreateContainerParameters
        {
            Image = agent.DockerImage,
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

        logger.LogInformation("Started Docker container {ContainerId} for agent session {SessionId}",
            container.ID, session.Id);

        return container.ID;
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
            authConfig: null,
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

    private static List<string> BuildEnvironment(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        GitRepository? gitRepository)
    {
        var env = new List<string>
        {
            $"ISSUEPIT_SESSION_ID={session.Id}",
            $"ISSUEPIT_ISSUE_ID={issue.Id}",
            $"ISSUEPIT_ISSUE_TITLE={issue.Title}",
            $"ISSUEPIT_ISSUE_BODY={issue.Body ?? string.Empty}",
            $"ISSUEPIT_AGENT_ID={agent.Id}",
            $"ISSUEPIT_SYSTEM_PROMPT={agent.SystemPrompt}",
        };

        if (issue.GitBranch is not null)
            env.Add($"ISSUEPIT_GIT_BRANCH={issue.GitBranch}");

        // Inject git repository info so the container can clone the repo on startup.
        if (gitRepository is not null)
        {
            env.Add($"ISSUEPIT_GIT_REMOTE_URL={gitRepository.RemoteUrl}");
            env.Add($"ISSUEPIT_GIT_DEFAULT_BRANCH={gitRepository.DefaultBranch}");
            if (!string.IsNullOrEmpty(gitRepository.AuthUsername))
                env.Add($"ISSUEPIT_GIT_AUTH_USERNAME={gitRepository.AuthUsername}");
            if (!string.IsNullOrEmpty(gitRepository.AuthToken))
                env.Add($"ISSUEPIT_GIT_AUTH_TOKEN={gitRepository.AuthToken}");
        }

        // Inject agent logins / API key credentials as environment variables
        foreach (var (key, value) in credentials)
            env.Add($"{key}={value}");

        // Runner-specific env vars (e.g. OPENCODE_SYSTEM_PROMPT, CODEX_SYSTEM_PROMPT)
        foreach (var (key, value) in RunnerCommandBuilder.BuildRunnerEnv(agent))
            env.Add($"{key}={value}");

        return env;
    }
}
