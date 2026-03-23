using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>Represents a parsed TRX test-result file collected from an artifact after a CI/CD run.</summary>
[Table("cicd_test_suites")]
public class CiCdTestSuite
{
    [Key]
    public Guid Id { get; set; }

    public Guid CiCdRunId { get; set; }

    [ForeignKey(nameof(CiCdRunId))]
    public CiCdRun CiCdRun { get; set; } = null!;

    /// <summary>The artifact name (e.g. "unit-test-results") or TRX file name.</summary>
    [MaxLength(500)]
    public string ArtifactName { get; set; } = string.Empty;

    /// <summary>
    /// The CI/CD job name (act <c>jobID</c>) that uploaded this artifact, inferred from run log entries.
    /// Null when the job name could not be determined (e.g. bare TRX files or legacy rows).
    /// </summary>
    [MaxLength(200)]
    public string? JobId { get; set; }

    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public int SkippedTests { get; set; }

    /// <summary>Total test duration in milliseconds.</summary>
    public double DurationMs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CiCdTestCase> TestCases { get; set; } = [];
}
