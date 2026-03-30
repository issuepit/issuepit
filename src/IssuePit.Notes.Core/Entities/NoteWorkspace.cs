using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Notes.Core.Enums;

namespace IssuePit.Notes.Core.Entities;

/// <summary>
/// A workspace groups notes together, analogous to a project.
/// Each workspace can be backed by a different storage engine and optionally linked to an IssuePit project.
/// </summary>
[Table("note_workspaces")]
public class NoteWorkspace
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Tenant that owns this workspace (multi-tenant isolation).</summary>
    public Guid TenantId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>Storage engine for this workspace.</summary>
    public NoteStorageEngine StorageEngine { get; set; } = NoteStorageEngine.Postgres;

    /// <summary>Optional reference to an IssuePit project (by ID). Not a FK — kept decoupled.</summary>
    public Guid? LinkedProjectId { get; set; }

    /// <summary>Optional Git repository URL for git-backed workspaces.</summary>
    [MaxLength(1000)]
    public string? GitRepositoryUrl { get; set; }

    /// <summary>Optional Git branch for git-backed workspaces.</summary>
    [MaxLength(200)]
    public string? GitBranch { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Note> Notes { get; set; } = [];
}
