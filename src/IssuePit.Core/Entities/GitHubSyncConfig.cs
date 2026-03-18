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
    /// Controls the direction of synchronisation:
    /// <list type="bullet">
    ///   <item><see cref="GitHubSyncMode.Import"/> — GitHub → IssuePit (default)</item>
    ///   <item><see cref="GitHubSyncMode.TwoWay"/> — GitHub ↔ IssuePit (bidirectional)</item>
    ///   <item><see cref="GitHubSyncMode.CreateOnGitHub"/> — new IssuePit issues are pushed to GitHub</item>
    /// </list>
    /// </summary>
    public GitHubSyncMode SyncMode { get; set; } = GitHubSyncMode.Import;

    /// <summary>
    /// Controls which content categories are included in a sync.
    /// Defaults to <see cref="GitHubSyncContent.Issues"/> for backward compatibility.
    /// Set to <see cref="GitHubSyncContent.All"/> to also import GitHub Actions runs.
    /// </summary>
    public GitHubSyncContent SyncContent { get; set; } = GitHubSyncContent.Issues;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
