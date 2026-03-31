using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>
/// Records a single auto-fetch polling cycle for a project's git repositories.
/// Created by <see cref="IssuePit.Api.Services.GitPollingService"/> each time it processes a project.
/// </summary>
[Table("git_repo_auto_fetch_runs")]
public class GitRepoAutoFetchRun
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    public GitHubSyncRunStatus Status { get; set; } = GitHubSyncRunStatus.Pending;

    /// <summary>Human-readable summary produced at the end of the run (e.g. "Fetched 2 repo(s), 1 new commit(s)").</summary>
    [MaxLength(500)]
    public string? Summary { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public ICollection<GitRepoAutoFetchRunLog> Logs { get; set; } = [];
}
