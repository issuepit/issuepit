using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>A git repository hosted by the IssuePit Git Server.</summary>
[Table("git_server_repos")]
public class GitServerRepo
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization Org { get; set; } = null!;

    /// <summary>Optional project this repo belongs to.</summary>
    public Guid? ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>
    /// Optional link to a <see cref="GitRepository"/> (external repo tracked by IssuePit).
    /// Deliberately no FK constraint to keep the link loose — the GitRepository may not exist.
    /// </summary>
    public Guid? GitRepositoryId { get; set; }

    /// <summary>URL-safe name used in git clone URLs (e.g. "my-repo" → .../my-repo.git).</summary>
    [Required, MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Default branch name.</summary>
    [MaxLength(200)]
    public string DefaultBranch { get; set; } = "main";

    /// <summary>Absolute filesystem path to the bare repository.</summary>
    [Required, MaxLength(500)]
    public string DiskPath { get; set; } = string.Empty;

    /// <summary>When true, the repo is read-only for all users (no pushes allowed).</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>When true, the repo was auto-created (temporary) and may be deleted automatically.</summary>
    public bool IsTemporary { get; set; }

    /// <summary>Default access level for authenticated org members who have no explicit permission entry.</summary>
    public IssuePit.Core.Enums.GitServerAccessLevel DefaultAccessLevel { get; set; } = IssuePit.Core.Enums.GitServerAccessLevel.Read;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }

    public ICollection<GitServerPermission> Permissions { get; set; } = [];
    public ICollection<GitServerBranchProtection> BranchProtections { get; set; } = [];
}
