using System.Diagnostics;
using System.Text.Json;
using IssuePit.Core.Entities;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>
/// Runs the GitHub Copilot CLI (<c>gh copilot suggest</c>) as the agent for the assigned issue.
///
/// Authentication is configured via environment variables injected from the organisation's API keys:
/// <list type="bullet">
///   <item><c>GITHUB_TOKEN</c> – Personal access token or GitHub Apps installation token (github.com).</item>
///   <item><c>GH_TOKEN</c> – Alternative token variable read by the <c>gh</c> CLI; automatically derived from
///     <c>GITHUB_TOKEN</c> when not explicitly provided.</item>
///   <item><c>GH_ENTERPRISE_TOKEN</c> + <c>GH_HOST</c> – GitHub Enterprise Server authentication.
///     <c>GH_HOST</c> can be set via the <c>GhHost</c> field in the runtime configuration.</item>
/// </list>
/// See: https://docs.github.com/en/copilot/how-tos/copilot-cli/set-up-copilot-cli/authenticate-copilot-cli
///
/// Expected optional <see cref="RuntimeConfiguration.Configuration"/> JSON:
/// <code>
/// {
///   "GhPath": "/usr/local/bin/gh",
///   "GhHost": "github.example.com"
/// }
/// </code>
///
/// CLI command reference: https://docs.github.com/en/copilot/reference/cli-command-reference
/// </summary>
public class CopilotCliAgentRuntime(ILogger<CopilotCliAgentRuntime> logger) : IAgentRuntime
{
    private const string DefaultGhPath = "gh";

    public Task<string> LaunchAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        RuntimeConfiguration? runtimeConfig,
        CancellationToken cancellationToken)
    {
        var ghPath = DefaultGhPath;
        string? ghHost = null;

        if (runtimeConfig is not null)
        {
            var config = JsonSerializer.Deserialize<CopilotCliConfig>(
                runtimeConfig.Configuration,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (!string.IsNullOrWhiteSpace(config?.GhPath))
                ghPath = config.GhPath;

            if (!string.IsNullOrWhiteSpace(config?.GhHost))
                ghHost = config.GhHost;
        }

        // Build the prompt from the agent's system prompt and the issue details.
        // CLI command reference: https://docs.github.com/en/copilot/reference/cli-command-reference
        var prompt = BuildPrompt(agent, issue);

        var startInfo = new ProcessStartInfo
        {
            FileName = ghPath,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("copilot");
        startInfo.ArgumentList.Add("suggest");
        startInfo.ArgumentList.Add(prompt);

        // Inject session context as environment variables
        startInfo.Environment["ISSUEPIT_SESSION_ID"] = session.Id.ToString();
        startInfo.Environment["ISSUEPIT_ISSUE_ID"] = issue.Id.ToString();
        startInfo.Environment["ISSUEPIT_ISSUE_TITLE"] = issue.Title;
        startInfo.Environment["ISSUEPIT_ISSUE_BODY"] = issue.Body ?? string.Empty;
        startInfo.Environment["ISSUEPIT_AGENT_ID"] = agent.Id.ToString();

        if (issue.GitBranch is not null)
            startInfo.Environment["ISSUEPIT_GIT_BRANCH"] = issue.GitBranch;

        // Authentication: inject credentials from API keys.
        // The gh CLI accepts GITHUB_TOKEN or GH_TOKEN for github.com.
        // For GHE: GH_ENTERPRISE_TOKEN + GH_HOST.
        // See: https://docs.github.com/en/copilot/how-tos/copilot-cli/set-up-copilot-cli/authenticate-copilot-cli
        foreach (var (key, value) in credentials)
            startInfo.Environment[key] = value;

        // Propagate GH_TOKEN from GITHUB_TOKEN when GH_TOKEN is not explicitly set,
        // as the gh CLI prefers GH_TOKEN over GITHUB_TOKEN.
        if (!credentials.ContainsKey("GH_TOKEN") && credentials.TryGetValue("GITHUB_TOKEN", out var token))
            startInfo.Environment["GH_TOKEN"] = token;

        // Set GH_HOST for GitHub Enterprise Server authentication (from runtime config).
        if (ghHost is not null)
            startInfo.Environment["GH_HOST"] = ghHost;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start gh CLI process at: {ghPath}");

        var pid = process.Id;
        logger.LogInformation("Started GitHub Copilot CLI agent process (PID {Pid}) for session {SessionId}",
            pid, session.Id);

        // Detach from the process — the agent runs independently.
        // Disposing the Process handle does not terminate the process itself.
        return Task.FromResult(pid.ToString());
    }

    /// <summary>
    /// Combines the agent's system prompt with the issue title and body to form the Copilot CLI prompt.
    /// </summary>
    private static string BuildPrompt(Agent agent, Issue issue)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(agent.SystemPrompt))
            parts.Add(agent.SystemPrompt);

        parts.Add($"Title: {issue.Title}");

        if (!string.IsNullOrWhiteSpace(issue.Body))
            parts.Add($"Description: {issue.Body}");

        return string.Join("\n\n", parts);
    }

    private record CopilotCliConfig(string? GhPath, string? GhHost);
}
