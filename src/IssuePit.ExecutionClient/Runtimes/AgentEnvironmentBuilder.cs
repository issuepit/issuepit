using IssuePit.Core.Entities;
using IssuePit.Core.Runners;
using System.Text.Json;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>
/// Builds the environment variable list passed to agent containers.
/// Shared between <see cref="DockerAgentRuntime"/> and <see cref="SshDockerAgentRuntime"/>
/// to keep the two runtimes in sync.
/// </summary>
internal static class AgentEnvironmentBuilder
{
    /// <summary>
    /// Returns the environment variables for an agent container as a list of
    /// <c>KEY=VALUE</c> strings suitable for Docker's <c>Env</c> parameter.
    /// </summary>
    public static List<string> Build(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        GitRepository? gitRepository,
        string? issuePitMcpUrl = null)
    {
        var env = new List<string>
        {
            $"ISSUEPIT_SESSION_ID={session.Id}",
            $"ISSUEPIT_ISSUE_ID={issue.Id}",
            $"ISSUEPIT_ISSUE_NUMBER={issue.Number}",
            $"ISSUEPIT_ISSUE_TITLE={issue.Title}",
            $"ISSUEPIT_ISSUE_BODY={issue.Body ?? string.Empty}",
            $"ISSUEPIT_AGENT_ID={agent.Id}",
            $"ISSUEPIT_PROJECT_ID={issue.ProjectId}",
            $"ISSUEPIT_SYSTEM_PROMPT={agent.SystemPrompt}",
        };

        if (issue.GitBranch is not null)
            env.Add($"ISSUEPIT_GIT_BRANCH={issue.GitBranch}");

        // Inform the entrypoint whether internet access is disabled (used for DNS logging display).
        env.Add($"ISSUEPIT_DISABLE_INTERNET={agent.DisableInternet.ToString().ToLowerInvariant()}");

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

        // Inject agent logins / API key credentials as environment variables.
        foreach (var (key, value) in credentials)
            env.Add($"{key}={value}");

        // Runner-specific env vars (e.g. OPENCODE_SYSTEM_PROMPT, CODEX_SYSTEM_PROMPT).
        foreach (var (key, value) in RunnerCommandBuilder.BuildRunnerEnv(agent))
            env.Add($"{key}={value}");

        // Inject the IssuePit MCP server URL so the entrypoint can write the opencode config.
        if (!string.IsNullOrWhiteSpace(issuePitMcpUrl))
            env.Add($"ISSUEPIT_MCP_URL={issuePitMcpUrl}");

        // Inject the agents list (current agent + children) as JSON so the entrypoint can configure
        // the CLI with nested agent modes. Only one level of nesting is supported.
        var agentsJson = BuildAgentsJson(agent);
        if (!string.IsNullOrEmpty(agentsJson))
            env.Add($"ISSUEPIT_OPENCODE_AGENTS_JSON={agentsJson}");

        return env;
    }

    /// <summary>
    /// Serialises the current agent and its direct children (one level only) into a JSON array
    /// for consumption by the container entrypoint when writing the opencode config.
    /// Format: [{ "name": "...", "model": "...", "prompt": "...", "agentType": "primary"|"subagent"|null }, ...]
    /// </summary>
    internal static string BuildAgentsJson(Agent agent)
    {
        var agents = new List<object>
        {
            new { name = agent.Name, model = agent.Model ?? string.Empty, prompt = agent.SystemPrompt, agentType = AgentTypeToString(agent.AgentType) },
        };

        foreach (var child in agent.ChildAgents)
            agents.Add(new { name = child.Name, model = child.Model ?? string.Empty, prompt = child.SystemPrompt, agentType = AgentTypeToString(child.AgentType) });

        return JsonSerializer.Serialize(agents);
    }

    /// <summary>Converts an <see cref="OpenCodeAgentType"/> to the opencode config string value.</summary>
    private static string? AgentTypeToString(IssuePit.Core.Enums.OpenCodeAgentType? agentType) => agentType switch
    {
        IssuePit.Core.Enums.OpenCodeAgentType.Primary => "primary",
        IssuePit.Core.Enums.OpenCodeAgentType.SubAgent => "subagent",
        _ => null,
    };
}
