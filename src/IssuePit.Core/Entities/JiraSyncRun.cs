using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>
/// Records a single Jira sync run for audit and debugging purposes.
/// Each manual or automatic sync creates one run record with associated log entries.
/// </summary>
[Table("jira_sync_runs")]
public class JiraSyncRun
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    public GitHubSyncRunStatus Status { get; set; } = GitHubSyncRunStatus.Pending;

    /// <summary>Human-readable summary produced at the end of the run (e.g. "5 imported, 0 updated").</summary>
    [MaxLength(500)]
    public string? Summary { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public ICollection<JiraSyncRunLog> Logs { get; set; } = [];
}
