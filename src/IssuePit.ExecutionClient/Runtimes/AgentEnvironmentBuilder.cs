using IssuePit.Core.Entities;
using IssuePit.Core.Runners;

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
        GitRepository? gitRepository)
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

        return env;
    }
}
