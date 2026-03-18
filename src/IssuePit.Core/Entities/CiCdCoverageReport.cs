using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>Represents a parsed Cobertura coverage report collected from an artifact after a CI/CD run.</summary>
[Table("cicd_coverage_reports")]
public class CiCdCoverageReport
{
    [Key]
    public Guid Id { get; set; }

    public Guid CiCdRunId { get; set; }

    [ForeignKey(nameof(CiCdRunId))]
    public CiCdRun CiCdRun { get; set; } = null!;

    /// <summary>The artifact name (e.g. "coverage-report") or file name.</summary>
    [MaxLength(500)]
    public string ArtifactName { get; set; } = string.Empty;

    /// <summary>Fraction of lines covered (0.0–1.0).</summary>
    public double LineRate { get; set; }

    /// <summary>Fraction of branches covered (0.0–1.0).</summary>
    public double BranchRate { get; set; }

    /// <summary>Number of lines covered.</summary>
    public int LinesCovered { get; set; }

    /// <summary>Total number of coverable lines.</summary>
    public int LinesValid { get; set; }

    /// <summary>Number of branches covered.</summary>
    public int BranchesCovered { get; set; }

    /// <summary>Total number of coverable branches.</summary>
    public int BranchesValid { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
