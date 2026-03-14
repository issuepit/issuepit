using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>
/// Tracks the mapping between a git branch or commit and an <see cref="Issue"/>.
/// Populated by the branch-detection background service which scans repositories
/// for issue references in branch names and commit messages.
/// </summary>
[Table("issue_git_mappings")]
public class IssueGitMapping
{
    [Key]
    public Guid Id { get; set; }

    public Guid IssueId { get; set; }

    [ForeignKey(nameof(IssueId))]
    public Issue Issue { get; set; } = null!;

    public Guid RepositoryId { get; set; }

    [ForeignKey(nameof(RepositoryId))]
    public GitRepository Repository { get; set; } = null!;

    /// <summary>Branch name that was mapped to this issue. Null when <see cref="Source"/> is <see cref="IssueGitMappingSource.CommitMessage"/>.</summary>
    [MaxLength(500)]
    public string? BranchName { get; set; }

    /// <summary>Commit SHA that was mapped to this issue. Null when <see cref="Source"/> is <see cref="IssueGitMappingSource.BranchName"/>.</summary>
    [MaxLength(100)]
    public string? CommitSha { get; set; }

    /// <summary>How the mapping was detected.</summary>
    public IssueGitMappingSource Source { get; set; }

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}
