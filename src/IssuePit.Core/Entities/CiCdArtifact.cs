using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>Represents an artifact produced by a CI/CD run (uploaded via actions/upload-artifact).</summary>
[Table("cicd_artifacts")]
public class CiCdArtifact
{
    [Key]
    public Guid Id { get; set; }

    public Guid CiCdRunId { get; set; }

    [ForeignKey(nameof(CiCdRunId))]
    public CiCdRun CiCdRun { get; set; } = null!;

    /// <summary>The artifact name as uploaded (top-level directory name in the artifact server path).</summary>
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Total size of all files in the artifact, in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Number of files contained in the artifact.</summary>
    public int FileCount { get; set; }

    /// <summary>S3 download URL for the artifact ZIP. Null when S3 upload is not configured.</summary>
    [MaxLength(2000)]
    public string? DownloadUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
