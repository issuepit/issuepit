using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>
/// Represents an authenticated session for a user, storing the GitHub OAuth
/// access token so it can be reused for agent integrations (gh CLI, Copilot, etc.).
/// </summary>
[Table("user_sessions")]
public class UserSession
{
    [Key]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    /// <summary>GitHub OAuth access token — stored with a "plain:" prefix until encryption is added.</summary>
    [Required]
    public string GitHubAccessToken { get; set; } = string.Empty;

    /// <summary>Unique JWT token ID (jti) — used to associate and revoke JWTs tied to this session.</summary>
    [Required, MaxLength(36)]
    public string JwtTokenId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }
}
