using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("issues")]
public class Issue
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    public string? Body { get; set; }

    public IssueStatus Status { get; set; } = IssueStatus.Backlog;

    public IssuePriority Priority { get; set; } = IssuePriority.NoPriority;

    public IssueType Type { get; set; } = IssueType.Issue;

    public int Number { get; set; }

    public Guid? ParentIssueId { get; set; }

    [ForeignKey(nameof(ParentIssueId))]
    public Issue? ParentIssue { get; set; }

    public ICollection<Issue> SubIssues { get; set; } = [];

    public ICollection<Label> Labels { get; set; } = [];

    public ICollection<IssueAssignee> Assignees { get; set; } = [];

    public ICollection<IssueLink> Links { get; set; } = [];

    public Guid? MilestoneId { get; set; }

    [ForeignKey(nameof(MilestoneId))]
    public Milestone? Milestone { get; set; }

    public int? GitHubIssueNumber { get; set; }

    public string? GitHubIssueUrl { get; set; }

    /// <summary>
    /// The issue number from an external tracker (e.g. GitHub issue #69, Jira ticket number).
    /// Used as the primary display ID when set.
    /// </summary>
    public int? ExternalId { get; set; }

    /// <summary>
    /// FK to the <see cref="IssueExternalSource"/> record that describes the external tracker
    /// (type, slug, URL). Null when the issue has no external counterpart.
    /// Display formatting is handled by <c>formatIssueId</c> in the frontend:
    /// GitHub issues render as bare numbers (e.g. <c>#69</c>), Jira-style sources as
    /// <c>#PROJ-42</c>.
    /// </summary>
    public Guid? ExternalSourceId { get; set; }

    /// <summary>Navigation property — included on demand in API queries.</summary>
    [ForeignKey(nameof(ExternalSourceId))]
    public IssueExternalSource? ExternalSource { get; set; }

    public string? GitBranch { get; set; }

    public int KanbanRank { get; set; }

    /// <summary>
    /// When true, agents and MCP tools are not allowed to move this issue between kanban columns.
    /// </summary>
    public bool PreventAgentMove { get; set; }

    /// <summary>
    /// When true, this issue is hidden from agents and MCP tools (excluded from list/get results in agent context).
    /// </summary>
    public bool HideFromAgents { get; set; }

    /// <summary>
    /// Number of times the orchestrator has interacted with this issue (both moves and skips).
    /// Incremented on every AI-driven orchestration action: each skip (<see cref="IssueEventType.KanbanOrchestrationSkipped"/>)
    /// and each AI-triggered transition (<see cref="IssueEventType.KanbanMoved"/> via TriggerTransition).
    /// Reset to 0 only on direct human moves (via the MoveIssue endpoint).
    /// Used by the orchestration loop limiter to prevent the AI from cycling an issue between states indefinitely.
    /// </summary>
    public int OrchestrationAttempts { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of the last activity on this issue, including comments.
    /// Updated whenever a comment is added, updated, or removed, as well as when the issue itself changes.
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Comments loaded for agent prompts. Not persisted — populated on demand before agent launch.
    /// </summary>
    [NotMapped]
    public IList<IssueComment> Comments { get; set; } = [];

    /// <summary>
    /// Sub-issues loaded for agent prompts. Not persisted — populated on demand before agent launch.
    /// </summary>
    [NotMapped]
    public IList<Issue> PromptSubIssues { get; set; } = [];

    /// <summary>
    /// Tasks loaded for agent prompts. Not persisted — populated on demand before agent launch.
    /// </summary>
    [NotMapped]
    public IList<IssueTask> PromptTasks { get; set; } = [];

    /// <summary>
    /// Linked issues loaded for agent prompts. Not persisted — populated on demand before agent launch.
    /// </summary>
    [NotMapped]
    public IList<IssueLink> PromptLinks { get; set; } = [];

    /// <summary>
    /// Attachments loaded for agent prompts. Not persisted — populated on demand before agent launch.
    /// </summary>
    [NotMapped]
    public IList<IssueAttachment> PromptAttachments { get; set; } = [];

    /// <summary>
    /// When set, the comment with this ID was the trigger for the current agent run.
    /// Not persisted — set on demand before agent launch.
    /// </summary>
    [NotMapped]
    public Guid? TriggeringCommentId { get; set; }

    /// <summary>
    /// Similar issues to include in the agent prompt. Not persisted — populated on demand when
    /// the triggering comment contains <c>#similar</c>.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public IList<SimilarIssuePair> PromptSimilarIssues { get; set; } = [];

    /// <summary>
    /// Recent CI/CD runs to include in the agent prompt. Not persisted — populated on demand when
    /// the triggering comment contains <c>#runs</c>.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public IList<CiCdRun> PromptCiCdRuns { get; set; } = [];
}
