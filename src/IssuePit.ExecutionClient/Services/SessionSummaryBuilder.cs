using System.Text;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.ExecutionClient.Services;

/// <summary>
/// Builds a structured Markdown summary from agent session logs.
/// Extracts errors, changes, and resolution approaches to serve as guidelines
/// for future agent runs on the same project.
/// </summary>
public static class SessionSummaryBuilder
{
    /// <summary>Maximum number of log lines to include in the error section.</summary>
    private const int MaxErrorLines = 20;

    /// <summary>Maximum total characters for the summary content.</summary>
    private const int MaxSummaryLength = 8000;

    /// <summary>
    /// Builds a Markdown summary note from session data and logs.
    /// </summary>
    public static (string Title, string Content) Build(
        AgentSession session,
        string? agentName,
        string? issueTitle,
        IReadOnlyList<AgentSessionLog> logs)
    {
        var title = BuildTitle(session, agentName, issueTitle);
        var content = BuildContent(session, agentName, issueTitle, logs);
        return (title, content);
    }

    private static string BuildTitle(AgentSession session, string? agentName, string? issueTitle)
    {
        var sb = new StringBuilder();
        sb.Append($"Session Summary — {session.Status}");
        if (!string.IsNullOrWhiteSpace(agentName))
            sb.Append($" — {agentName}");
        if (!string.IsNullOrWhiteSpace(issueTitle))
            sb.Append($" — {issueTitle}");
        // Truncate to 500 chars (Note title limit)
        if (sb.Length > 500) sb.Length = 500;
        return sb.ToString();
    }

    private static string BuildContent(
        AgentSession session,
        string? agentName,
        string? issueTitle,
        IReadOnlyList<AgentSessionLog> logs)
    {
        var sb = new StringBuilder();

        // ── Session Overview ────────────────────────────────────────────
        sb.AppendLine("## Session Overview");
        sb.AppendLine();
        sb.AppendLine($"- **Session ID**: `{session.Id}`");
        sb.AppendLine($"- **Status**: {session.Status}");
        if (!string.IsNullOrWhiteSpace(agentName))
            sb.AppendLine($"- **Agent**: {agentName}");
        if (!string.IsNullOrWhiteSpace(issueTitle))
            sb.AppendLine($"- **Issue**: {issueTitle}");
        if (!string.IsNullOrEmpty(session.GitBranch))
            sb.AppendLine($"- **Branch**: `{session.GitBranch}`");
        if (!string.IsNullOrEmpty(session.CommitSha))
            sb.AppendLine($"- **Commit**: `{session.CommitSha}`");
        sb.AppendLine($"- **Started**: {session.StartedAt:yyyy-MM-dd HH:mm:ss} UTC");
        if (session.EndedAt.HasValue)
        {
            sb.AppendLine($"- **Ended**: {session.EndedAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
            var duration = session.EndedAt.Value - session.StartedAt;
            sb.AppendLine($"- **Duration**: {duration.TotalMinutes:F1} minutes");
        }
        sb.AppendLine();

        // ── Errors Encountered ──────────────────────────────────────────
        var errorLines = logs
            .Where(l => l.Stream == LogStream.Stderr || IsErrorLine(l.Line))
            .Select(l => l.Line.Trim())
            .Where(l => l.Length > 0)
            .Distinct()
            .Take(MaxErrorLines)
            .ToList();

        if (errorLines.Count > 0)
        {
            sb.AppendLine("## Errors Encountered");
            sb.AppendLine();
            sb.AppendLine("```");
            foreach (var line in errorLines)
                sb.AppendLine(line);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        // ── Key Actions / Steps ─────────────────────────────────────────
        var stepLines = ExtractStepSummaries(logs);
        if (stepLines.Count > 0)
        {
            sb.AppendLine("## Key Actions");
            sb.AppendLine();
            foreach (var step in stepLines)
                sb.AppendLine($"- {step}");
            sb.AppendLine();
        }

        // ── Guidelines for Future Runs ──────────────────────────────────
        sb.AppendLine("## Guidelines");
        sb.AppendLine();
        if (session.Status == AgentSessionStatus.Succeeded)
        {
            sb.AppendLine("This session completed successfully. The approach taken can be referenced for similar issues.");
        }
        else if (session.Status == AgentSessionStatus.Failed)
        {
            sb.AppendLine("This session failed. Review the errors above and consider:");
            sb.AppendLine("- Whether the same approach should be retried with fixes");
            sb.AppendLine("- If a different strategy might avoid these errors");
            if (errorLines.Count > 0)
                sb.AppendLine("- The specific error patterns to watch for in future runs");
        }
        else
        {
            sb.AppendLine($"This session ended with status: {session.Status}.");
        }

        // Truncate if too long
        var result = sb.ToString();
        if (result.Length > MaxSummaryLength)
            result = result[..MaxSummaryLength] + "\n\n*(truncated)*";
        return result;
    }

    private static bool IsErrorLine(string line)
    {
        return line.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase)
               || line.Contains("[FAIL]", StringComparison.OrdinalIgnoreCase)
               || line.Contains("error:", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Error:", StringComparison.Ordinal);
    }

    /// <summary>
    /// Extracts high-level step descriptions from log lines by looking for
    /// section markers and key action indicators.
    /// </summary>
    private static List<string> ExtractStepSummaries(IReadOnlyList<AgentSessionLog> logs)
    {
        var steps = new List<string>();
        string? currentSection = null;

        foreach (var log in logs)
        {
            // Track section changes
            if (log.Section is not null && log.Section.ToString() != currentSection)
            {
                currentSection = log.Section.ToString();
                steps.Add($"**{FormatSectionName(currentSection!)}**");
            }

            // Extract key action lines
            if (log.Line.Contains("[INFO]", StringComparison.OrdinalIgnoreCase) && IsSignificantAction(log.Line))
                steps.Add(CleanLogLine(log.Line));
        }

        // Limit to most important steps
        return steps.Take(15).ToList();
    }

    private static bool IsSignificantAction(string line)
    {
        return line.Contains("completed", StringComparison.OrdinalIgnoreCase)
               || line.Contains("succeeded", StringComparison.OrdinalIgnoreCase)
               || line.Contains("failed", StringComparison.OrdinalIgnoreCase)
               || line.Contains("triggered", StringComparison.OrdinalIgnoreCase)
               || line.Contains("pushed", StringComparison.OrdinalIgnoreCase)
               || line.Contains("committed", StringComparison.OrdinalIgnoreCase)
               || line.Contains("created", StringComparison.OrdinalIgnoreCase)
               || line.Contains("status:", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatSectionName(string section)
    {
        // Convert PascalCase/camelCase to space-separated words
        return System.Text.RegularExpressions.Regex.Replace(section, "([a-z])([A-Z])", "$1 $2");
    }

    private static string CleanLogLine(string line)
    {
        // Remove timestamp prefix and [INFO] tag
        var idx = line.IndexOf("[INFO]", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return line[(idx + 6)..].Trim();
        return line.Trim();
    }
}
