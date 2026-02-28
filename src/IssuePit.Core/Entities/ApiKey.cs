using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("api_keys")]
public class ApiKey
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization Organization { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public ApiKeyProvider Provider { get; set; }

    /// <summary>Encrypted value — never returned in API responses.</summary>
    [Required]
    public string EncryptedValue { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }
}
