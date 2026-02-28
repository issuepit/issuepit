using Docker.DotNet;
using Docker.DotNet.Models;
using IssuePit.Core.Entities;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>Runs the agent inside a local Docker container with Docker-in-Docker (DinD) support.</summary>
public class DockerAgentRuntime(ILogger<DockerAgentRuntime> logger, DockerClient dockerClient) : IAgentRuntime
{
    public async Task<string> LaunchAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken)
    {
        var env = BuildEnvironment(session, agent, issue, credentials);

        logger.LogInformation("Creating Docker container from image {Image} for agent {AgentId}",
            agent.DockerImage, agent.Id);

        var createParams = new CreateContainerParameters
        {
            Image = agent.DockerImage,
            Env = env,
            HostConfig = new HostConfig
            {
                // Mount Docker socket for Docker-in-Docker (DinD) support
                Binds = ["/var/run/docker.sock:/var/run/docker.sock"],
                AutoRemove = true,
            },
            Labels = new Dictionary<string, string>
            {
                ["issuepit.session-id"] = session.Id.ToString(),
                ["issuepit.issue-id"] = issue.Id.ToString(),
                ["issuepit.agent-id"] = agent.Id.ToString(),
            },
        };

        var container = await dockerClient.Containers.CreateContainerAsync(
            createParams, cancellationToken);

        await dockerClient.Containers.StartContainerAsync(
            container.ID, new ContainerStartParameters(), cancellationToken);

        logger.LogInformation("Started Docker container {ContainerId} for agent session {SessionId}",
            container.ID, session.Id);

        return container.ID;
    }

    private static List<string> BuildEnvironment(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials)
    {
        var env = new List<string>
        {
            $"ISSUEPIT_SESSION_ID={session.Id}",
            $"ISSUEPIT_ISSUE_ID={issue.Id}",
            $"ISSUEPIT_ISSUE_TITLE={issue.Title}",
            $"ISSUEPIT_ISSUE_BODY={issue.Body ?? string.Empty}",
            $"ISSUEPIT_AGENT_ID={agent.Id}",
        };

        if (issue.GitBranch is not null)
            env.Add($"ISSUEPIT_GIT_BRANCH={issue.GitBranch}");

        // Inject agent logins / API key credentials as environment variables
        foreach (var (key, value) in credentials)
            env.Add($"{key}={value}");

        return env;
    }
}
