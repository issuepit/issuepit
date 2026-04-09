namespace IssuePit.Core.Entities;

/// <summary>
/// Lightweight DTO carrying a guideline note's title and content for injection into agent prompts.
/// Not persisted — used only as a transient carrier between <c>IssueWorker</c> and
/// <c>RunnerCommandBuilder</c>.
/// </summary>
public sealed record GuidelineNotePrompt(string Title, string Content);
