using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>Records a single similar-issue detection run for a project.</summary>
[Table("similar_issue_runs")]
public class SimilarIssueRun
{
    [Key]
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;
    public GitHubSyncRunStatus Status { get; set; } = GitHubSyncRunStatus.Pending;
    [MaxLength(500)]
    public string? Summary { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public ICollection<SimilarIssueRunLog> Logs { get; set; } = [];
}
