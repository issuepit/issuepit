using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("project_properties")]
public class ProjectProperty
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>Display name of the property (e.g. "Due Date", "Reporter").</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>The data type of the property value.</summary>
    public ProjectPropertyType Type { get; set; }

    /// <summary>Whether a value is required for every issue in this project.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Default value as a string (serialized based on Type).</summary>
    [MaxLength(1000)]
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Constraints for the value, serialized as JSON.
    /// For Enum: JSON array of allowed strings e.g. ["frontend","backend","infra"].
    /// For Number/Date: JSON object with optional min/max e.g. {"min":0,"max":100}.
    /// For Text: JSON object with optional maxLength e.g. {"maxLength":500}.
    /// </summary>
    public string? AllowedValues { get; set; }

    /// <summary>Display order within the project.</summary>
    public int Position { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<IssuePropertyValue> Values { get; set; } = [];
}
