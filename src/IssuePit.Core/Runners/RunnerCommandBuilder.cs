using System.Text;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Runners;

/// <summary>
/// Builds CLI invocation arguments for supported runner types.
///
/// Supported runners:
/// - opencode CLI: https://opencode.ai/docs
/// - Codex CLI: https://github.com/openai/codex-cli
/// - GitHub Copilot CLI: https://docs.github.com/copilot/github-copilot-in-the-cli
/// </summary>
public static class RunnerCommandBuilder
{
    /// <summary>
    /// Returns the additional CLI arguments as a shell-escaped string to append after the base command.
    /// Suitable for shell-based runtimes (native process, SSH).
    /// Returns an empty string when no runner type is set (legacy entrypoint behaviour).
    /// </summary>
    public static string BuildArgs(Agent agent, Issue issue)
    {
        if (agent.RunnerType is null)
            return string.Empty;

        var task = BuildTaskPrompt(issue);

        return agent.RunnerType switch
        {
            RunnerType.OpenCode => BuildOpenCodeArgs(agent, task),
            RunnerType.Codex => BuildCodexArgs(agent, task),
            RunnerType.GitHubCopilotCli => BuildCopilotArgs(task),
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Returns the additional CLI arguments as a raw string list for use with the Docker API.
    /// Suitable for Docker-based runtimes where shell escaping is not needed.
    /// Returns an empty list when no runner type is set (legacy entrypoint behaviour).
    /// </summary>
    public static IReadOnlyList<string> BuildArgsList(Agent agent, Issue issue)
    {
        if (agent.RunnerType is null)
            return [];

        var task = BuildTaskPrompt(issue);

        return agent.RunnerType switch
        {
            RunnerType.OpenCode => BuildOpenCodeArgsList(agent, task),
            RunnerType.Codex => BuildCodexArgsList(agent, task),
            RunnerType.GitHubCopilotCli => BuildCopilotArgsList(task),
            _ => [],
        };
    }

    /// <summary>
    /// Returns runner-specific environment variables to inject alongside the standard ISSUEPIT_* vars.
    /// This allows runners to pick up the system prompt via their own env var convention.
    /// </summary>
    public static IReadOnlyDictionary<string, string> BuildRunnerEnv(Agent agent)
    {
        if (agent.RunnerType is null)
            return new Dictionary<string, string>();

        return agent.RunnerType switch
        {
            // opencode reads the system prompt from OPENCODE_SYSTEM_PROMPT
            // https://opencode.ai/docs
            RunnerType.OpenCode => new Dictionary<string, string>
            {
                ["OPENCODE_SYSTEM_PROMPT"] = agent.SystemPrompt,
            },
            // codex reads the system prompt from CODEX_SYSTEM_PROMPT
            // https://github.com/openai/codex-cli
            RunnerType.Codex => new Dictionary<string, string>
            {
                ["CODEX_SYSTEM_PROMPT"] = agent.SystemPrompt,
            },
            _ => new Dictionary<string, string>(),
        };
    }

    /// <summary>
    /// Build shell-escaped args for the opencode CLI.
    /// Usage: opencode [--model MODEL] TASK
    /// https://opencode.ai/docs
    /// </summary>
    private static string BuildOpenCodeArgs(Agent agent, string task)
    {
        var args = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(agent.Model))
            args.Append($" --model {EscapeShellArg(agent.Model)}");
        args.Append($" {EscapeShellArg(task)}");
        return args.ToString().TrimStart();
    }

    private static IReadOnlyList<string> BuildOpenCodeArgsList(Agent agent, string task)
    {
        var args = new List<string>();
        if (!string.IsNullOrWhiteSpace(agent.Model))
        {
            args.Add("--model");
            args.Add(agent.Model);
        }
        args.Add(task);
        return args;
    }

    /// <summary>
    /// Build shell-escaped args for the OpenAI Codex CLI.
    /// Usage: codex [--model MODEL] [--full-auto] TASK
    /// https://github.com/openai/codex-cli
    /// </summary>
    private static string BuildCodexArgs(Agent agent, string task)
    {
        var args = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(agent.Model))
            args.Append($" --model {EscapeShellArg(agent.Model)}");
        args.Append(" --full-auto");
        args.Append($" {EscapeShellArg(task)}");
        return args.ToString().TrimStart();
    }

    private static IReadOnlyList<string> BuildCodexArgsList(Agent agent, string task)
    {
        var args = new List<string>();
        if (!string.IsNullOrWhiteSpace(agent.Model))
        {
            args.Add("--model");
            args.Add(agent.Model);
        }
        args.Add("--full-auto");
        args.Add(task);
        return args;
    }

    /// <summary>
    /// Build shell-escaped args for the GitHub Copilot CLI.
    /// Usage: gh copilot suggest TASK
    /// https://docs.github.com/copilot/github-copilot-in-the-cli
    /// </summary>
    private static string BuildCopilotArgs(string task) =>
        // GitHub Copilot CLI does not support --model selection at this time
        $"suggest {EscapeShellArg(task)}";

    private static IReadOnlyList<string> BuildCopilotArgsList(string task) =>
        // GitHub Copilot CLI does not support --model selection at this time
        ["suggest", task];

    /// <summary>Formats the issue title and body into a single task prompt string.</summary>
    private static string BuildTaskPrompt(Issue issue)
    {
        var sb = new StringBuilder();
        sb.Append($"Task: {issue.Title}");
        if (!string.IsNullOrWhiteSpace(issue.Body))
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(issue.Body);
        }
        return sb.ToString();
    }

    /// <summary>Wraps a value in single quotes and escapes embedded single quotes for POSIX shell.</summary>
    private static string EscapeShellArg(string value) =>
        $"'{value.Replace("'", "'\\''")}'";
}
