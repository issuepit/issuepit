using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("tenants")]
public class Tenant
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(253)]
    public string Hostname { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? DatabaseConnectionString { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
