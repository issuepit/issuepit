using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

    public string? GitBranch { get; set; }

    public int KanbanRank { get; set; }

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
}
