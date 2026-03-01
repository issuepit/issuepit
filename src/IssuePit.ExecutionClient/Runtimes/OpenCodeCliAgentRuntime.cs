using System.Diagnostics;
using System.Text.Json;
using IssuePit.Core.Entities;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>
/// Runs the OpenCode CLI tool (https://github.com/opencode/cli) as a background process.
///
/// OpenCode is an open-source, AI-powered code agent that can be used as an alternative
/// to Copilot. This runtime invokes the CLI executable directly on the host, passing
/// session context via environment variables.
///
/// Authentication is handled via the <c>OPENCODE_CLI_TOKEN</c> environment variable
/// (mapped from the stored API key with provider <c>OpenCodeCli</c>) or via an existing
/// CLI session store created by running <c>opencode auth login</c> beforehand.
///
/// Expected <see cref="RuntimeConfiguration.Configuration"/> JSON:
/// <code>
/// {
///   "CliPath": "/usr/local/bin/opencode",
///   "Model": "openrouter/gpt-4o-code"
/// }
/// </code>
///
/// Runtime hint: When running inside Docker or over SSH, ensure the CLI binary and any
/// CLI session stores (e.g. <c>~/.config/opencode</c>) are available inside the execution
/// environment. Use environment detection (e.g. <c>SSH_CLIENT</c>, <c>container</c> env vars)
/// to configure mount paths or copy credentials appropriately.
/// </summary>
public class OpenCodeCliAgentRuntime(ILogger<OpenCodeCliAgentRuntime> logger) : IAgentRuntime
{
    public Task<string> LaunchAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        RuntimeConfiguration? runtimeConfig,
        CancellationToken cancellationToken)
    {
        if (runtimeConfig is null)
            throw new InvalidOperationException("OpenCodeCliAgentRuntime requires a RuntimeConfiguration.");

        var config = JsonSerializer.Deserialize<OpenCodeCliConfig>(runtimeConfig.Configuration,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Could not deserialize OpenCodeCli RuntimeConfiguration.");

        if (string.IsNullOrWhiteSpace(config.CliPath))
            throw new InvalidOperationException("OpenCodeCli RuntimeConfiguration missing 'CliPath'.");

        if (!File.Exists(config.CliPath))
            throw new InvalidOperationException($"OpenCode CLI not found at: {config.CliPath}");

        var startInfo = new ProcessStartInfo
        {
            FileName = config.CliPath,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true,
        };

        // Inject session context as environment variables
        startInfo.Environment["ISSUEPIT_SESSION_ID"] = session.Id.ToString();
        startInfo.Environment["ISSUEPIT_ISSUE_ID"] = issue.Id.ToString();
        startInfo.Environment["ISSUEPIT_ISSUE_TITLE"] = issue.Title;
        startInfo.Environment["ISSUEPIT_ISSUE_BODY"] = issue.Body ?? string.Empty;
        startInfo.Environment["ISSUEPIT_AGENT_ID"] = agent.Id.ToString();

        if (issue.GitBranch is not null)
            startInfo.Environment["ISSUEPIT_GIT_BRANCH"] = issue.GitBranch;

        // Pass model override if configured
        if (!string.IsNullOrWhiteSpace(config.Model))
            startInfo.Environment["OPENCODE_MODEL"] = config.Model;

        // Inject all org credentials (includes OPENCODE_CLI_TOKEN, OPENROUTER_API_KEY, etc.)
        foreach (var (key, value) in credentials)
            startInfo.Environment[key] = value;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start OpenCode CLI process: {config.CliPath}");

        var pid = process.Id;
        logger.LogInformation(
            "Started OpenCode CLI process (PID {Pid}) for session {SessionId} (model: {Model})",
            pid, session.Id, config.Model ?? "default");

        // Detach — the agent runs independently.
        // Disposing the Process handle does not terminate the underlying OS process.
        return Task.FromResult(pid.ToString());
    }

    private record OpenCodeCliConfig(string CliPath, string? Model);
}
