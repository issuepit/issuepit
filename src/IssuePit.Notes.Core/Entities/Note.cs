using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Notes.Core.Entities;

/// <summary>
/// A single note within a workspace. Stores markdown content with wiki-style linking support.
/// </summary>
[Table("notes")]
public class Note
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Tenant that owns this note (multi-tenant isolation).</summary>
    public Guid TenantId { get; set; }

    public Guid WorkspaceId { get; set; }

    [ForeignKey(nameof(WorkspaceId))]
    public NoteWorkspace Workspace { get; set; } = null!;

    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Markdown content of the note.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Monotonically increasing version number. Incremented on each save.
    /// Used for optimistic concurrency: clients send the current version and the server
    /// rejects the update if the stored version has advanced (another user/system saved first).
    /// </summary>
    public long Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Links originating from this note.</summary>
    public ICollection<NoteLink> OutgoingLinks { get; set; } = [];

    /// <summary>Links pointing to this note from other notes.</summary>
    public ICollection<NoteLink> IncomingLinks { get; set; } = [];
}
