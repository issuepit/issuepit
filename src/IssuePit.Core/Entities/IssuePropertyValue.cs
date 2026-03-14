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

    public Guid CustomPropertyId { get; set; }

    [ForeignKey(nameof(CustomPropertyId))]
    public CustomProperty CustomProperty { get; set; } = null!;

    [MaxLength(2000)]
    public string? Value { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
