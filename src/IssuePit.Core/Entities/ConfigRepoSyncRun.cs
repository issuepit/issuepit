using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>
/// Records a single config-repo sync run for a tenant.
/// Created by <see cref="IssuePit.Api.Services.ConfigRepoSyncService"/> each time it processes a tenant's config repository.
/// </summary>
[Table("config_repo_sync_runs")]
public class ConfigRepoSyncRun
{
    [Key]
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant Tenant { get; set; } = null!;

    public GitHubSyncRunStatus Status { get; set; } = GitHubSyncRunStatus.Pending;

    /// <summary>Human-readable summary produced at the end of the run (e.g. "3 files processed, 1 warning").</summary>
    [MaxLength(500)]
    public string? Summary { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public ICollection<ConfigRepoSyncRunLog> Logs { get; set; } = [];
}
