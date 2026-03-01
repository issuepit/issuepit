using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>Hourly snapshot of key metric counts for a project.</summary>
[Table("project_metric_snapshots")]
public class ProjectMetricSnapshot
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>UTC timestamp when this snapshot was recorded (truncated to the hour).</summary>
    public DateTime RecordedAt { get; set; }

    public int OpenIssues { get; set; }

    public int InProgressIssues { get; set; }

    public int DoneIssues { get; set; }

    public int TotalAgentRuns { get; set; }

    public int TotalCiCdRuns { get; set; }
}
