using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("organizations")]
public class Organization
{
    [Key]
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant Tenant { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Maximum number of concurrent CI/CD runners for this organization. 0 means unlimited.</summary>
    public int MaxConcurrentRunners { get; set; } = 0;

    /// <summary>Docker runner image override for act. Null means use the global default.</summary>
    public string? ActRunnerImage { get; set; }

    /// <summary>Newline-separated KEY=VALUE pairs passed as <c>--env</c> arguments to <c>act</c> on each run.</summary>
    public string? ActEnv { get; set; }

    /// <summary>Newline-separated KEY=VALUE pairs passed as <c>--secret</c> arguments to <c>act</c> on each run.</summary>
    public string? ActSecrets { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
