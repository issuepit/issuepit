using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>A single line of captured output from a CI/CD run.</summary>
[Table("cicd_run_logs")]
public class CiCdRunLog
{
    [Key]
    public Guid Id { get; set; }

    public Guid CiCdRunId { get; set; }

    [ForeignKey(nameof(CiCdRunId))]
    public CiCdRun CiCdRun { get; set; } = null!;

    [Required]
    public string Line { get; set; } = string.Empty;

    public LogStream Stream { get; set; } = LogStream.Stdout;

    /// <summary>Job name extracted from act's JSON output (e.g. "build", "test"). Null for non-JSON log lines.</summary>
    [MaxLength(200)]
    public string? JobId { get; set; }

    /// <summary>
    /// Step name extracted from act's JSON <c>stage</c> field (e.g. "Set up job", "Main actions/checkout@v4").
    /// Null for non-JSON log lines or lines that do not belong to a specific step.
    /// </summary>
    [MaxLength(500)]
    public string? StepId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
