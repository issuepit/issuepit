using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("issue_property_values")]
public class IssuePropertyValue
{
    [Key]
    public Guid Id { get; set; }

    public Guid IssueId { get; set; }

    [ForeignKey(nameof(IssueId))]
    public Issue Issue { get; set; } = null!;

    public Guid PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public ProjectProperty Property { get; set; } = null!;

    /// <summary>The serialized value for this property on this issue.</summary>
    [MaxLength(2000)]
    public string? Value { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
