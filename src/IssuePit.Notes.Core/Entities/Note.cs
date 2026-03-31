using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Notes.Core.Enums;

namespace IssuePit.Notes.Core.Entities;

[Table("notes")]
public class Note
{
    [Key]
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid NotebookId { get; set; }

    [ForeignKey(nameof(NotebookId))]
    public Notebook Notebook { get; set; } = null!;

    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Markdown content of the note.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    public NoteStatus Status { get; set; } = NoteStatus.Draft;

    /// <summary>
    /// Monotonically increasing version counter for optimistic concurrency (plain storage).
    /// Incremented on each save; callers must supply the current version to avoid overwriting
    /// concurrent edits.
    /// </summary>
    public long Version { get; set; } = 1;

    /// <summary>
    /// Slug for wiki-style [[...]] linking (derived from title).
    /// </summary>
    [MaxLength(500)]
    public string Slug { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<NoteLink> OutgoingLinks { get; set; } = [];

    public ICollection<NoteLink> IncomingLinks { get; set; } = [];

    public ICollection<NoteTagMapping> TagMappings { get; set; } = [];

    /// <summary>
    /// CRDT event log: all OT operations applied to this note in order.
    /// </summary>
    public ICollection<NoteOperation> Operations { get; set; } = [];
}
