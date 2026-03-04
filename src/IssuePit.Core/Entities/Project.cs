using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("projects")]
public class Project
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization Organization { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? GitHubRepo { get; set; }

    /// <summary>Whether to mount the repository workspace into the Docker runner container. Default is true.</summary>
    public bool MountRepositoryInDocker { get; set; } = true;

    /// <summary>Maximum number of concurrent CI/CD runners for this project. 0 means unlimited.</summary>
    public int MaxConcurrentRunners { get; set; } = 0;

    /// <summary>Newline-separated KEY=VALUE pairs passed as <c>--env</c> arguments to <c>act</c> on each run.</summary>
    public string? ActEnv { get; set; }

    /// <summary>Newline-separated KEY=VALUE pairs passed as <c>--secret</c> arguments to <c>act</c> on each run.</summary>
    public string? ActSecrets { get; set; }

    /// <summary>Whether this project serves as the organization-wide common agenda (global goal tracker across all projects).</summary>
    public bool IsAgenda { get; set; } = false;
  
    /// <summary>Docker runner image override for act. Null means use the org or global default.</summary>
    public string? ActRunnerImage { get; set; }

    /// <summary>
    /// Docker image caching strategy for DinD containers. Null means use the org or global default
    /// (configured via <c>CiCd__DindCache__Strategy</c>, defaulting to <c>Volume</c>).
    /// </summary>
    public DindCacheStrategy? DindCacheStrategy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
