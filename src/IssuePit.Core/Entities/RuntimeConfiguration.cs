using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("runtime_configurations")]
public class RuntimeConfiguration
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization Organization { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public RuntimeType Type { get; set; }

    /// <summary>JSON blob holding type-specific connection parameters (host, port, SSH key ref, etc.).</summary>
    [Required]
    public string Configuration { get; set; } = "{}";

    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
