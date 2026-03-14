using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>
/// Stores the GitHub synchronisation configuration for a project.
/// One record per project (created on first save).
/// </summary>
[Table("github_sync_configs")]
public class GitHubSyncConfig
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>GitHub identity (PAT) used for API calls.</summary>
    public Guid? GitHubIdentityId { get; set; }

    [ForeignKey(nameof(GitHubIdentityId))]
    public GitHubIdentity? GitHubIdentity { get; set; }

    /// <summary>GitHub repository in <c>owner/repo</c> format (e.g. "acme/backend").</summary>
    [MaxLength(300)]
    public string? GitHubRepo { get; set; }

    /// <summary>Controls when automatic sync is triggered.</summary>
    public GitHubSyncTriggerMode TriggerMode { get; set; } = GitHubSyncTriggerMode.Off;

    /// <summary>
    /// When <c>true</c>, newly created IssuePit issues are automatically pushed to GitHub as new issues.
    /// Hidden behind a feature flag — disabled by default.
    /// </summary>
    public bool AutoCreateOnGitHub { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
