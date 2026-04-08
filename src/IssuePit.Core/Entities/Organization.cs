using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// Raw JSON mapping of CI/CD field names (camelCase) to the config source file that set them.
    /// Populated by <see cref="IssuePit.Api.Services.ConfigRepoApplier"/>. Null when no config file has set any CI/CD field.
    /// Use <see cref="ConfigFieldSources"/> to access the parsed dictionary.
    /// </summary>
    [MaxLength(10000)]
    [Column("config_field_sources")]
    [JsonIgnore]
    public string? ConfigFieldSourcesJson { get; set; }

    /// <summary>
    /// Per-field config source mapping parsed from <see cref="ConfigFieldSourcesJson"/>.
    /// Keys are camelCase field names (e.g. "actRunnerImage", "actEnv"). Values are config file names.
    /// Not persisted by EF Core — read-only computed from <see cref="ConfigFieldSourcesJson"/>.
    /// </summary>
    [NotMapped]
    [JsonPropertyName("configFieldSources")]
    public Dictionary<string, string>? ConfigFieldSources =>
        ConfigFieldSourcesJson is null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(ConfigFieldSourcesJson);

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

    /// <summary>
    /// Maximum number of CI/CD → agent-fix loop iterations after the initial agent run.
    /// Null means use the system default (3).
    /// </summary>
    public int? MaxCiCdLoopCount { get; set; }

    /// <summary>
    /// When <c>true</c> (the default), the execution client appends IssuePit metadata
    /// as <a href="https://git-scm.com/docs/git-interpret-trailers">git trailers</a> to all
    /// agent-created commits before pushing (agent name, LLM model, issue link).
    /// Set to <c>false</c> to disable trailer injection for all projects in this organization.
    /// Per-project overrides are available via <see cref="IssuePit.Core.Entities.Project.AddGitTrailers"/>.
    /// </summary>
    public bool AddGitTrailers { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
