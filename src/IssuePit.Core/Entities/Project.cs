using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
