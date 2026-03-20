using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>
/// Records a single branch-detection scan run for a project.
/// Created by <see cref="IssuePit.Api.Services.BranchDetectionService"/> each time it processes a project's repositories.
/// </summary>
[Table("branch_detection_runs")]
public class BranchDetectionRun
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    public GitHubSyncRunStatus Status { get; set; } = GitHubSyncRunStatus.Pending;

    /// <summary>Human-readable summary produced at the end of the run (e.g. "5 new mapping(s)").</summary>
    [MaxLength(500)]
    public string? Summary { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public ICollection<BranchDetectionRunLog> Logs { get; set; } = [];
}
