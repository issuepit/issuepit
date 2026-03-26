using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("organizations")]
public class Organization
{
    [Key]
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant Tenant { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Maximum number of concurrent CI/CD runners for this organization. 0 means unlimited.</summary>
    public int MaxConcurrentRunners { get; set; } = 0;

    /// <summary>Maximum number of concurrent jobs within a single CI/CD run (passed as --concurrent-jobs to act). null means use the system default (4). 0 means unlimited.</summary>
    public int? ConcurrentJobs { get; set; }

    /// <summary>Docker runner image override for act. Null means use the global default.</summary>
    public string? ActRunnerImage { get; set; }

    /// <summary>Filename of the JSON5 config file that last set <see cref="ActRunnerImage"/> via the config repo. Null when set manually.</summary>
    [MaxLength(500)]
    public string? ActRunnerImageSourceFile { get; set; }

    /// <summary>Newline-separated KEY=VALUE pairs passed as <c>--env</c> arguments to <c>act</c> on each run.</summary>
    public string? ActEnv { get; set; }

    /// <summary>Newline-separated KEY=VALUE pairs passed as <c>--secret</c> arguments to <c>act</c> on each run.</summary>
    public string? ActSecrets { get; set; }

    /// <summary>
    /// Host path for the act action/repo cache directory (passed as <c>--action-cache-path</c>).
    /// When set, previously cloned actions are reused across runs. Null means use the system default.
    /// </summary>
    public string? ActionCachePath { get; set; }

    /// <summary>
    /// When <c>true</c>, enables act's new OCI-based action cache (<c>--use-new-action-cache</c>).
    /// Effective only when <see cref="ActionCachePath"/> is also set.
    /// </summary>
    public bool UseNewActionCache { get; set; } = false;

    /// <summary>
    /// When <c>true</c>, passes <c>--action-offline-mode</c> to act so it uses only locally
    /// cached actions without hitting the network.
    /// </summary>
    public bool ActionOfflineMode { get; set; } = false;

    /// <summary>
    /// Newline-separated list of <c>owner/repo@ref=/local/path</c> mappings passed as
    /// <c>--local-repository</c> arguments to <c>act</c>. Allows rerouting private or
    /// internal reusable workflows/actions to local paths.
    /// </summary>
    public string? LocalRepositories { get; set; }

    /// <summary>
    /// Newline-separated list of step names or <c>job:step</c> pairs passed as
    /// <c>--skip-step</c> arguments to <c>act</c>. Steps matching these entries are skipped
    /// on every run. Useful to disable push/deploy steps in non-production environments.
    /// </summary>
    public string? SkipSteps { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
