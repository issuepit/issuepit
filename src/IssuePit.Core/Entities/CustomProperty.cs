using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("custom_properties")]
public class CustomProperty
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public CustomPropertyType Type { get; set; }

    public bool IsRequired { get; set; }

    [MaxLength(500)]
    public string? DefaultValue { get; set; }

    /// <summary>JSON array of allowed values (for Enum type) or {min, max} object for range types.</summary>
    public string? AllowedValues { get; set; }

    public int Position { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<IssuePropertyValue> Values { get; set; } = [];
}
