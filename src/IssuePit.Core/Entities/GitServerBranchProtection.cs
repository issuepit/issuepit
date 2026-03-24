using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>Defines branch protection rules for a hosted git repository.</summary>
[Table("git_server_branch_protections")]
public class GitServerBranchProtection
{
    [Key]
    public Guid Id { get; set; }

    public Guid RepoId { get; set; }

    [ForeignKey(nameof(RepoId))]
    public GitServerRepo Repo { get; set; } = null!;

    /// <summary>Glob pattern matching branch names (e.g. "main", "release/*").</summary>
    [Required, MaxLength(200)]
    public string Pattern { get; set; } = string.Empty;

    /// <summary>When true, force pushes to matching branches are rejected.</summary>
    public bool DisallowForcePush { get; set; }

    /// <summary>When true, direct pushes to matching branches are rejected (requires PR).</summary>
    public bool RequirePullRequest { get; set; }

    /// <summary>When true, admins can bypass this protection rule.</summary>
    public bool AllowAdminBypass { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
