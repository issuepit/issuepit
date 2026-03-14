using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>
/// Records a single GitHub sync run for audit and debugging purposes.
/// Each manual or automatic sync creates one run record with associated log entries.
/// </summary>
[Table("github_sync_runs")]
public class GitHubSyncRun
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    public GitHubSyncRunStatus Status { get; set; } = GitHubSyncRunStatus.Pending;

    /// <summary>Human-readable summary produced at the end of the run (e.g. "3 imported, 1 updated, 2 conflicts").</summary>
    [MaxLength(500)]
    public string? Summary { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public ICollection<GitHubSyncRunLog> Logs { get; set; } = [];
}
