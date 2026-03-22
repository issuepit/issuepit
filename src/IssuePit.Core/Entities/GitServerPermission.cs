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

    /// <summary>User granted the permission. Null if the grant is for an API key.</summary>
    public Guid? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>API key granted the permission. Null if the grant is for a user.</summary>
    public Guid? ApiKeyId { get; set; }

    [ForeignKey(nameof(ApiKeyId))]
    public ApiKey? ApiKey { get; set; }

    public GitServerAccessLevel AccessLevel { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
