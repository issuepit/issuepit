using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("users")]
public class User
{
    [Key]
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant Tenant { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required, MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Hashed password for local authentication. Null for SSO-only accounts.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Whether this user has system-wide admin privileges.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Short bio or description shown on the user's profile.</summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
