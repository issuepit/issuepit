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

    /// <summary>GitHub user ID — used to link GitHub OAuth identity to this user.</summary>
    [MaxLength(50)]
    public string? GitHubId { get; set; }

    /// <summary>GitHub avatar URL for display in the UI.</summary>
    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
