using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>
/// Represents a GitHub SSO identity linked to a local <see cref="User"/>.
/// One user can have multiple GitHub identities (e.g., per organisation/tenant).
/// The OAuth access token is stored encrypted and never exposed directly in API responses.
/// </summary>
[Table("github_identities")]
public class GitHubIdentity
{
    [Key]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    /// <summary>GitHub's numeric user ID — stable across username changes.</summary>
    [Required, MaxLength(20)]
    public string GitHubId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string GitHubUsername { get; set; } = string.Empty;

    [MaxLength(254)]
    public string? GitHubEmail { get; set; }

    /// <summary>
    /// The GitHub OAuth access token, stored encrypted via ASP.NET Core Data Protection.
    /// Retrieve the plaintext value through <c>AuthController.GetToken</c> only for
    /// authenticated sessions. Agents receive it via a dedicated secure endpoint.
    /// </summary>
    [Required]
    public string EncryptedToken { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
