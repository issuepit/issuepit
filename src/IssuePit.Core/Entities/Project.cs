using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("projects")]
public class Project
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization Organization { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? GitHubRepo { get; set; }

    /// <summary>Whether to mount the repository workspace into the Docker runner container. Default is true.</summary>
    public bool MountRepositoryInDocker { get; set; } = true;

    /// <summary>Maximum number of concurrent CI/CD runners for this project. 0 means unlimited.</summary>
    public int MaxConcurrentRunners { get; set; } = 0;

    /// <summary>Maximum number of concurrent jobs within a single CI/CD run (passed as --concurrent-jobs to act). null means inherit from org. 0 means unlimited.</summary>
    public int? ConcurrentJobs { get; set; }

    /// <summary>Newline-separated KEY=VALUE pairs passed as <c>--env</c> arguments to <c>act</c> on each run.</summary>
    public string? ActEnv { get; set; }

    /// <summary>Newline-separated KEY=VALUE pairs passed as <c>--secret</c> arguments to <c>act</c> on each run.</summary>
    public string? ActSecrets { get; set; }

    /// <summary>Whether this project serves as the organization-wide common agenda (global goal tracker across all projects).</summary>
    public bool IsAgenda { get; set; } = false;
  
    /// <summary>Docker runner image override for act. Null means use the org or global default.</summary>
    public string? ActRunnerImage { get; set; }

    /// <summary>
    /// Host path for the act action/repo cache directory (passed as <c>--action-cache-path</c>).
    /// Null means inherit from the organization setting.
    /// </summary>
    public string? ActionCachePath { get; set; }

    /// <summary>
    /// When <c>true</c>, enables act's new OCI-based action cache (<c>--use-new-action-cache</c>).
    /// Null means inherit from the organization setting.
    /// </summary>
    public bool? UseNewActionCache { get; set; }

    /// <summary>
    /// When <c>true</c>, passes <c>--action-offline-mode</c> to act so it uses only locally
    /// cached actions. Null means inherit from the organization setting.
    /// </summary>
    public bool? ActionOfflineMode { get; set; }

    /// <summary>
    /// Newline-separated list of <c>owner/repo@ref=/local/path</c> mappings passed as
    /// <c>--local-repository</c> arguments to <c>act</c>. Null means inherit from the org.
    /// </summary>
    public string? LocalRepositories { get; set; }

    /// <summary>
    /// Short project key used as prefix for issue IDs in the UI (e.g. "IP" yields "IP-123").
    /// Auto-generated from the project name using initials. Must be unique within the organization.
    /// </summary>
    [MaxLength(10)]
    public string? IssueKey { get; set; }

    /// <summary>
    /// Offset added to issue numbers when displayed in the UI.
    /// Useful to avoid ID collisions when syncing with external trackers like Jira or GitHub (e.g. set to 10000).
    /// </summary>
    public int IssueNumberOffset { get; set; } = 0;

    /// <summary>
    /// When <c>true</c>, triggered CI/CD runs are placed in the
    /// <see cref="IssuePit.Core.Enums.CiCdRunStatus.WaitingForApproval"/> state and are NOT
    /// dispatched to the CI/CD worker until explicitly approved via
    /// <c>POST /api/cicd-runs/{id}/approve</c>.
    ///
    /// Use this on demo / seeded projects that have a linked git repository so that the
    /// automatic git-polling trigger does not launch real CI/CD runners without human intent.
    /// </summary>
    public bool RequiresRunApproval { get; set; } = false;

    /// <summary>
    /// Optional accent color for the project (hex, e.g. "#4c6ef5").
    /// Used to highlight the project in sidebar menus and breadcrumbs.
    /// </summary>
    [MaxLength(7)]
    public string? Color { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProjectSkill> ProjectSkills { get; set; } = [];
}
