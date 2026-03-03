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

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
