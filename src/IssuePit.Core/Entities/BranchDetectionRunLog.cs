using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>A single log line emitted during a <see cref="BranchDetectionRun"/>.</summary>
[Table("branch_detection_run_logs")]
public class BranchDetectionRunLog
{
    [Key]
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    [ForeignKey(nameof(RunId))]
    public BranchDetectionRun Run { get; set; } = null!;

    public GitHubSyncLogLevel Level { get; set; } = GitHubSyncLogLevel.Info;

    [Required]
    public string Message { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
