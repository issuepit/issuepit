using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>Grants a specific user or API key an explicit access level on a hosted git repository.</summary>
[Table("git_server_permissions")]
public class GitServerPermission
{
    [Key]
    public Guid Id { get; set; }

    public Guid RepoId { get; set; }

    [ForeignKey(nameof(RepoId))]
    public GitServerRepo Repo { get; set; } = null!;

    /// <summary>User granted the permission. Null if the grant is for an API key.
    /// No FK constraint — permissions may reference users that are not yet registered.</summary>
    public Guid? UserId { get; set; }

    /// <summary>API key granted the permission. Null if the grant is for a user.
    /// No FK constraint — permissions may reference keys that have not yet been created.</summary>
    public Guid? ApiKeyId { get; set; }

    public GitServerAccessLevel AccessLevel { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
