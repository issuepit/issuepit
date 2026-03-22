using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>A Personal Access Token (PAT) for authenticating against the IssuePit Git Server.</summary>
[Table("git_pats")]
public class GitPat
{
    [Key]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    /// <summary>Human-readable label for this token.</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>BCrypt hash of the raw token value.</summary>
    [Required]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>First 8 characters of the raw token (e.g. "ip_a1b2c3") shown for identification.</summary>
    [Required, MaxLength(8)]
    public string Prefix { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When null, the token never expires.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Updated on each successful authentication.</summary>
    public DateTime? LastUsedAt { get; set; }
}
