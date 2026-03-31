using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Notes.Core.Enums;

namespace IssuePit.Notes.Core.Entities;

[Table("notebooks")]
public class Notebook
{
    [Key]
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Optional link to an IssuePit project (by external project ID).
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Storage backend for this notebook's notes.
    /// </summary>
    public StorageProvider StorageProvider { get; set; } = StorageProvider.Postgres;

    /// <summary>
    /// Optional Git repository URL for git-backed notebooks.
    /// </summary>
    [MaxLength(500)]
    public string? GitRepoUrl { get; set; }

    /// <summary>
    /// Git branch to use (default: main).
    /// </summary>
    [MaxLength(200)]
    public string? GitBranch { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Note> Notes { get; set; } = [];

    public ICollection<NoteTag> Tags { get; set; } = [];
}
