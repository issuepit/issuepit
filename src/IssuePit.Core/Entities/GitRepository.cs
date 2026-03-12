using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("git_repositories")]
public class GitRepository
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>The remote git URL to clone from (https or git protocol).</summary>
    [Required, MaxLength(500)]
    public string RemoteUrl { get; set; } = string.Empty;

    /// <summary>Local filesystem path where the bare/working clone is stored.</summary>
    [MaxLength(500)]
    public string? LocalPath { get; set; }

    /// <summary>Default branch name (e.g. "main" or "master").</summary>
    [MaxLength(200)]
    public string DefaultBranch { get; set; } = "main";

    /// <summary>Optional username for HTTP basic auth or token-based auth.</summary>
    [MaxLength(200)]
    public string? AuthUsername { get; set; }

    /// <summary>Optional PAT/token for authentication.
    /// TODO: Encrypt using ASP.NET Core Data Protection (see <see cref="GitHubIdentity.EncryptedToken"/> for the pattern).</summary>
    [MaxLength(500)]
    public string? AuthToken { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastFetchedAt { get; set; }

    /// <summary>The last commit SHA on <see cref="DefaultBranch"/> for which a CI/CD run was triggered.</summary>
    [MaxLength(200)]
    public string? LastKnownCommitSha { get; set; }

    /// <summary>Current polling/health status of this repository.</summary>
    public GitRepoStatus Status { get; set; } = GitRepoStatus.Active;

    /// <summary>Human-readable message describing why the repo was disabled or throttled.</summary>
    [MaxLength(1000)]
    public string? StatusMessage { get; set; }

    /// <summary>When set, the repo will be skipped by the poller until this time (throttle window).</summary>
    public DateTime? ThrottledUntil { get; set; }

    /// <summary>How this remote is used by agents and the release pipeline.</summary>
    public GitOriginMode Mode { get; set; } = GitOriginMode.Working;
}
