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
    public static string BuildArgs(Agent agent, Issue issue, IReadOnlyList<IssueComment>? comments = null)
    {
        if (agent.RunnerType is null)
            return string.Empty;

        var task = BuildTaskPrompt(issue, comments);

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
    /// <para>
    /// When <paramref name="forkSessionId"/> is provided and the runner supports session forking
    /// (opencode: <c>--session &lt;id&gt; --fork</c>), the fix run will continue from the given
    /// session so it retains full conversation context and workspace state.
    /// </para>
    /// <para>
    /// When <paramref name="continueSessionId"/> is provided the run resumes the same opencode
    /// session without forking (<c>--session &lt;id&gt;</c> only). Use this for the initial run
    /// of a new agent session that should continue a preserved previous session.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> BuildArgsList(Agent agent, Issue issue, string? forkSessionId = null, string? continueSessionId = null, IReadOnlyList<IssueComment>? comments = null)
    {
        if (agent.RunnerType is null)
            return [];

        var task = BuildTaskPrompt(issue, comments);

        return agent.RunnerType switch
        {
            RunnerType.OpenCode => BuildOpenCodeArgsList(agent, task, forkSessionId, continueSessionId),
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
    /// Usage: opencode run [--session ID --fork] [--model MODEL] TASK
    /// https://opencode.ai/docs/cli/#run-1
    /// </summary>
    private static string BuildOpenCodeArgs(Agent agent, string task)
    {
        var args = new StringBuilder("run");
        if (!string.IsNullOrWhiteSpace(agent.Model))
            args.Append($" --model {EscapeShellArg(agent.Model)}");
        args.Append($" {EscapeShellArg(task)}");
        return args.ToString();
    }

    private static IReadOnlyList<string> BuildOpenCodeArgsList(Agent agent, string task, string? forkSessionId = null, string? continueSessionId = null)
    {
        var args = new List<string> { "opencode", "run" };
        if (!string.IsNullOrWhiteSpace(forkSessionId))
        {
            // Continue the previous opencode session and fork it so this run is a child branch.
            // This gives the fix run full conversation context from the session that made the
            // original changes. https://opencode.ai/docs/cli/#run-1
            args.Add("--session");
            args.Add(forkSessionId);
            args.Add("--fork");
        }
        else if (!string.IsNullOrWhiteSpace(continueSessionId))
        {
            // Resume the same session without forking — used when starting a fresh container run
            // that should continue a preserved previous session (the opencode DB was injected).
            args.Add("--session");
            args.Add(continueSessionId);
        }
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
        var args = new List<string> { "codex" };
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
        ["gh", "copilot", "suggest", task];

    /// <summary>
    /// Maximum total character count for comments included in the task prompt.
    /// When exceeded, older comments are dropped and a truncation note is prepended.
    /// </summary>
    public const int MaxCommentsLength = 8000;

    /// <summary>
    /// Formats the issue context into a structured XML prompt for the agent.
    /// Reads sub-issues, tasks, linked issues, attachments, and the triggering comment ID
    /// from the <see cref="Issue"/>'s <c>NotMapped</c> prompt-context properties
    /// (<see cref="Issue.PromptSubIssues"/>, <see cref="Issue.PromptTasks"/>,
    /// <see cref="Issue.PromptLinks"/>, <see cref="Issue.PromptAttachments"/>,
    /// <see cref="Issue.TriggeringCommentId"/>).
    /// When a <see cref="Issue.TriggeringCommentId"/> is set, that comment is marked as the
    /// new instruction that triggered the agent run.
    /// </summary>
    public static string BuildTaskPrompt(Issue issue, IReadOnlyList<IssueComment>? comments = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<issue>");

        // Metadata
        sb.AppendLine("  <metadata>");
        sb.AppendLine($"    <number>{issue.Number}</number>");
        sb.AppendLine($"    <title>{EscapeXml(issue.Title)}</title>");
        if (issue.Status != default)
            sb.AppendLine($"    <status>{issue.Status}</status>");
        if (issue.Type != default)
            sb.AppendLine($"    <type>{issue.Type}</type>");
        if (issue.Priority != default)
            sb.AppendLine($"    <priority>{issue.Priority}</priority>");
        sb.AppendLine("  </metadata>");

        // Body
        if (!string.IsNullOrWhiteSpace(issue.Body))
        {
            sb.AppendLine("  <description>");
            sb.AppendLine(issue.Body.Trim());
            sb.AppendLine("  </description>");
        }

        // Sub-issues
        if (issue.PromptSubIssues.Count > 0)
        {
            sb.AppendLine("  <sub_issues>");
            foreach (var sub in issue.PromptSubIssues)
                sb.AppendLine($"    <sub_issue number=\"{sub.Number}\" status=\"{sub.Status}\">{EscapeXml(sub.Title)}</sub_issue>");
            sb.AppendLine("  </sub_issues>");
        }

        // Tasks
        if (issue.PromptTasks.Count > 0)
        {
            sb.AppendLine("  <tasks>");
            foreach (var task in issue.PromptTasks)
                sb.AppendLine($"    <task status=\"{task.Status}\">{EscapeXml(task.Title)}</task>");
            sb.AppendLine("  </tasks>");
        }

        // Linked issues
        if (issue.PromptLinks.Count > 0)
        {
            sb.AppendLine("  <linked_issues>");
            foreach (var link in issue.PromptLinks)
            {
                var linked = link.TargetIssue;
                if (linked is not null)
                    sb.AppendLine($"    <linked_issue number=\"{linked.Number}\" link_type=\"{link.LinkType}\">{EscapeXml(linked.Title)}</linked_issue>");
            }
            sb.AppendLine("  </linked_issues>");
        }

        // Attachments
        if (issue.PromptAttachments.Count > 0)
        {
            sb.AppendLine("  <attachments>");
            foreach (var att in issue.PromptAttachments)
                sb.AppendLine($"    <attachment content_type=\"{EscapeXml(att.ContentType)}\" url=\"{EscapeXml(att.FileUrl)}\">{EscapeXml(att.FileName)}</attachment>");
            sb.AppendLine("  </attachments>");
        }

        // Similar issues (only included when the triggering comment contained #similar)
        if (issue.PromptSimilarIssues.Count > 0)
        {
            sb.AppendLine("  <similar_issues>");
            foreach (var pair in issue.PromptSimilarIssues)
            {
                var similar = pair.SimilarIssue;
                if (similar is not null)
                {
                    var attrs = $"number=\"{similar.Number}\" score=\"{pair.Score:F2}\"";
                    if (!string.IsNullOrWhiteSpace(pair.Reason))
                        attrs += $" reason=\"{EscapeXml(pair.Reason)}\"";
                    sb.AppendLine($"    <similar_issue {attrs}>{EscapeXml(similar.Title)}</similar_issue>");
                }
            }
            sb.AppendLine("  </similar_issues>");
        }

        // CI/CD runs (only included when the triggering comment contained #runs)
        if (issue.PromptCiCdRuns.Count > 0)
        {
            sb.AppendLine($"  <cicd_runs limited_last_x=\"{issue.PromptCiCdRuns.Count}\">");
            foreach (var run in issue.PromptCiCdRuns)
            {
                var attrs = $"status=\"{run.Status}\" started_at=\"{run.StartedAt:yyyy-MM-ddTHH:mm:ssZ}\"";
                if (!string.IsNullOrWhiteSpace(run.Branch))
                    attrs += $" branch=\"{EscapeXml(run.Branch)}\"";
                if (!string.IsNullOrWhiteSpace(run.CommitSha))
                    attrs += $" commit=\"{EscapeXml(run.CommitSha[..Math.Min(8, run.CommitSha.Length)])}\"";
                if (!string.IsNullOrWhiteSpace(run.Workflow))
                    attrs += $" workflow=\"{EscapeXml(run.Workflow)}\"";
                sb.AppendLine($"    <cicd_run {attrs} />");
            }
            sb.AppendLine("  </cicd_runs>");
        }

        // Comments
        if (comments is { Count: > 0 })
        {
            sb.AppendLine("  <comments>");
            foreach (var comment in comments)
            {
                var author = comment.User?.Username ?? "Unknown";
                var isNew = issue.TriggeringCommentId.HasValue && comment.Id == issue.TriggeringCommentId.Value;
                if (isNew)
                    sb.AppendLine($"    <comment author=\"{EscapeXml(author)}\" date=\"{comment.CreatedAt:yyyy-MM-dd}\" is_new=\"true\">");
                else
                    sb.AppendLine($"    <comment author=\"{EscapeXml(author)}\" date=\"{comment.CreatedAt:yyyy-MM-dd}\">");
                sb.AppendLine(comment.Body.Trim());
                sb.AppendLine("    </comment>");
            }
            sb.AppendLine("  </comments>");
        }

        if (issue.TriggeringCommentId.HasValue)
        {
            sb.AppendLine("  <instruction>");
            sb.AppendLine("    The comment marked with is_new=\"true\" is the new instruction that triggered this agent run. Focus on fulfilling that request.");
            sb.AppendLine("  </instruction>");
        }

        sb.AppendLine("</issue>");
        sb.AppendLine();
        sb.Append("**Important:** Commit your changes after each meaningful step and make a final commit when the task is complete.");

        return sb.ToString().TrimEnd();
    }

    /// <summary>Escapes special XML characters in a string.</summary>
    private static string EscapeXml(string value) =>
        value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");

    /// <summary>Wraps a value in single quotes and escapes embedded single quotes for POSIX shell.</summary>
    private static string EscapeShellArg(string value) =>
        $"'{value.Replace("'", "'\\''")}'";
}
