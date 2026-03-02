using System.Net.Http.Json;
using System.Text.Json;
using IssuePit.Core.Entities;
using IssuePit.Core.Runners;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>
/// Uses Alibaba OpenSandbox (https://github.com/alibaba/OpenSandbox) as the agent execution environment.
/// 
/// OpenSandbox exposes a REST API that creates isolated sandbox environments.
/// This runtime POSTs to the /api/sandboxes endpoint to create a new sandbox
/// with the agent container image and injects issue context as environment variables.
/// 
/// Expected <see cref="RuntimeConfiguration.Configuration"/> JSON:
/// <code>
/// {
///   "Endpoint": "http://opensandbox-host:8080",
///   "ApiKey": "optional-api-key"
/// }
/// </code>
/// </summary>
public class OpenSandboxAgentRuntime(
    ILogger<OpenSandboxAgentRuntime> logger,
    IHttpClientFactory httpClientFactory) : IAgentRuntime
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
        if (runtimeConfig is null)
            throw new InvalidOperationException("OpenSandboxAgentRuntime requires a RuntimeConfiguration.");

        var config = JsonSerializer.Deserialize<OpenSandboxConfig>(runtimeConfig.Configuration,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Could not deserialize OpenSandbox RuntimeConfiguration.");

        if (string.IsNullOrWhiteSpace(config.Endpoint))
            throw new InvalidOperationException("OpenSandbox RuntimeConfiguration missing 'Endpoint'.");

        var sandboxId = await CreateSandboxAsync(session, agent, issue, credentials, gitRepository, config, cancellationToken);
        return sandboxId;
    }

    private async Task<string> CreateSandboxAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        GitRepository? gitRepository,
        OpenSandboxConfig config,
        CancellationToken cancellationToken)
    {
        using var http = httpClientFactory.CreateClient();

        if (!string.IsNullOrWhiteSpace(config.ApiKey))
            http.DefaultRequestHeaders.Add("X-Api-Key", config.ApiKey);

        var env = BuildEnvironment(session, agent, issue, credentials, gitRepository);

        var requestBody = new
        {
            image = agent.DockerImage,
            env,
            labels = new Dictionary<string, string>
            {
                ["issuepit.session-id"] = session.Id.ToString(),
                ["issuepit.issue-id"] = issue.Id.ToString(),
                ["issuepit.agent-id"] = agent.Id.ToString(),
            },
        };

        logger.LogInformation("Creating OpenSandbox sandbox from image {Image} for session {SessionId}",
            agent.DockerImage, session.Id);

        var sandboxesUrl = $"{config.Endpoint.TrimEnd('/')}/api/sandboxes";
        var response = await http.PostAsJsonAsync(sandboxesUrl, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenSandboxCreateResponse>(
            cancellationToken: cancellationToken);

        var sandboxId = result?.Id ?? throw new InvalidOperationException("OpenSandbox API did not return a sandbox ID.");

        logger.LogInformation("Created OpenSandbox sandbox {SandboxId} for session {SessionId}",
            sandboxId, session.Id);

        return sandboxId;
    }

    private static Dictionary<string, string> BuildEnvironment(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        GitRepository? gitRepository)
    {
        var env = new Dictionary<string, string>
        {
            ["ISSUEPIT_SESSION_ID"] = session.Id.ToString(),
            ["ISSUEPIT_ISSUE_ID"] = issue.Id.ToString(),
            ["ISSUEPIT_ISSUE_TITLE"] = issue.Title,
            ["ISSUEPIT_ISSUE_BODY"] = issue.Body ?? string.Empty,
            ["ISSUEPIT_AGENT_ID"] = agent.Id.ToString(),
            ["ISSUEPIT_SYSTEM_PROMPT"] = agent.SystemPrompt,
        };

        if (issue.GitBranch is not null)
            env["ISSUEPIT_GIT_BRANCH"] = issue.GitBranch;

        if (gitRepository is not null)
        {
            env["ISSUEPIT_GIT_REMOTE_URL"] = gitRepository.RemoteUrl;
            env["ISSUEPIT_GIT_DEFAULT_BRANCH"] = gitRepository.DefaultBranch;
            if (!string.IsNullOrEmpty(gitRepository.AuthUsername))
                env["ISSUEPIT_GIT_AUTH_USERNAME"] = gitRepository.AuthUsername;
            if (!string.IsNullOrEmpty(gitRepository.AuthToken))
                env["ISSUEPIT_GIT_AUTH_TOKEN"] = gitRepository.AuthToken;
        }

        // Inject agent logins / API key credentials
        foreach (var (key, value) in credentials)
            env[key] = value;

        // Runner-specific env vars (e.g. OPENCODE_SYSTEM_PROMPT, CODEX_SYSTEM_PROMPT)
        foreach (var (key, value) in RunnerCommandBuilder.BuildRunnerEnv(agent))
            env[key] = value;

        return env;
    }

    private record OpenSandboxConfig(string Endpoint, string? ApiKey);

    private record OpenSandboxCreateResponse(string Id);
}
