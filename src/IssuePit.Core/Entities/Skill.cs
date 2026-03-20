using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>
/// Represents a reusable skill (system prompt) that can be assigned to agent modes.
/// Skills can be version-tracked via a backing git repository.
/// </summary>
[Table("skills")]
public class Skill
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization Organization { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>The skill content / system prompt text.</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>Optional git repository URL used to version-track and sync this skill.</summary>
    [MaxLength(500)]
    public string? GitRepoUrl { get; set; }

    /// <summary>Optional subdirectory path within the git repository (for sparse-checkout).</summary>
    [MaxLength(500)]
    public string? GitSubDir { get; set; }

    /// <summary>Optional branch name to pin the skill to a specific branch.</summary>
    [MaxLength(200)]
    public string? GitBranch { get; set; }

    /// <summary>Optional commit SHA to pin the skill to a specific commit.</summary>
    [MaxLength(200)]
    public string? GitSha { get; set; }

    /// <summary>Optional username for HTTP basic auth on the git repository.</summary>
    [MaxLength(200)]
    public string? GitAuthUsername { get; set; }

    /// <summary>
    /// Optional PAT/token for authenticating against the git repository.
    /// TODO: Encrypt using ASP.NET Core Data Protection (see <see cref="GitHubIdentity.EncryptedToken"/> for the pattern).
    /// </summary>
    [MaxLength(500)]
    public string? GitAuthToken { get; set; }

    /// <summary>Current synchronisation status with the backing git repository.</summary>
    public SkillSyncStatus SyncStatus { get; set; } = SkillSyncStatus.None;

    /// <summary>Human-readable message describing the last sync result or any error.</summary>
    [MaxLength(1000)]
    public string? SyncMessage { get; set; }

    /// <summary>When the skill content was last synchronised with the git repository.</summary>
    public DateTime? LastSyncedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AgentSkill> AgentSkills { get; set; } = [];
    public ICollection<ProjectSkill> ProjectSkills { get; set; } = [];
}
