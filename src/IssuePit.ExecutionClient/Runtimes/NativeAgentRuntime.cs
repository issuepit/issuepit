using System.Diagnostics;
using System.Text.Json;
using IssuePit.Core.Entities;
using IssuePit.Core.Runners;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>
/// Runs the agent executable directly on the host machine as a background process (bare-metal).
/// 
/// Expected <see cref="RuntimeConfiguration.Configuration"/> JSON:
/// <code>
/// {
///   "Command": "/usr/local/bin/opencode"
/// }
/// </code>
/// </summary>
public class NativeAgentRuntime(ILogger<NativeAgentRuntime> logger) : IAgentRuntime
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
            throw new InvalidOperationException("NativeAgentRuntime requires a RuntimeConfiguration.");

        var config = JsonSerializer.Deserialize<NativeConfig>(runtimeConfig.Configuration,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Could not deserialize Native RuntimeConfiguration.");

        if (string.IsNullOrWhiteSpace(config.Command))
            throw new InvalidOperationException("Native RuntimeConfiguration missing 'Command'.");

        if (!File.Exists(config.Command))
            throw new InvalidOperationException($"Agent command not found: {config.Command}");

        var runnerArgs = RunnerCommandBuilder.BuildArgs(agent, issue);

        var startInfo = new ProcessStartInfo
        {
            FileName = config.Command,
            Arguments = runnerArgs,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true,
        };

        // Inject session context and credentials as environment variables
        startInfo.Environment["ISSUEPIT_SESSION_ID"] = session.Id.ToString();
        startInfo.Environment["ISSUEPIT_ISSUE_ID"] = issue.Id.ToString();
        startInfo.Environment["ISSUEPIT_ISSUE_TITLE"] = issue.Title;
        startInfo.Environment["ISSUEPIT_ISSUE_BODY"] = issue.Body ?? string.Empty;
        startInfo.Environment["ISSUEPIT_AGENT_ID"] = agent.Id.ToString();
        startInfo.Environment["ISSUEPIT_SYSTEM_PROMPT"] = agent.SystemPrompt;

        if (issue.GitBranch is not null)
            startInfo.Environment["ISSUEPIT_GIT_BRANCH"] = issue.GitBranch;

        foreach (var (key, value) in credentials)
            startInfo.Environment[key] = value;

        // Runner-specific env vars (e.g. OPENCODE_SYSTEM_PROMPT, CODEX_SYSTEM_PROMPT)
        foreach (var (key, value) in RunnerCommandBuilder.BuildRunnerEnv(agent))
            startInfo.Environment[key] = value;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {config.Command}");

        var pid = process.Id;
        logger.LogInformation("Started native agent process (PID {Pid}) for session {SessionId}",
            pid, session.Id);

        // Detach from the process — the agent runs independently.
        // Disposing the Process handle does not terminate the process itself.
        return Task.FromResult(pid.ToString());
    }

    private record NativeConfig(string Command);
}
