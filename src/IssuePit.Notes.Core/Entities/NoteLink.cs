using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Notes.Core.Enums;

namespace IssuePit.Notes.Core.Entities;

/// <summary>
/// Represents a link from a note to another note or to an external IssuePit entity.
/// Links are extracted from the markdown content's [[...]] wiki-style syntax.
/// </summary>
[Table("note_links")]
public class NoteLink
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>The note this link originates from.</summary>
    public Guid SourceNoteId { get; set; }

    [ForeignKey(nameof(SourceNoteId))]
    public Note SourceNote { get; set; } = null!;

    /// <summary>Type of the link target.</summary>
    public NoteLinkType LinkType { get; set; }

    /// <summary>
    /// For NoteToNote links: the target note ID.
    /// Null when the link target is not a note.
    /// </summary>
    public Guid? TargetNoteId { get; set; }

    [ForeignKey(nameof(TargetNoteId))]
    public Note? TargetNote { get; set; }

    /// <summary>
    /// For links to external IssuePit entities (project, issue, todo): the target entity ID.
    /// Not a FK — kept decoupled from the main IssuePit database.
    /// </summary>
    public Guid? TargetEntityId { get; set; }

    /// <summary>
    /// The raw link text as it appeared in the markdown (e.g. "[[My Note]]" or "[[issue:123]]").
    /// Preserved for display and re-linking.
    /// </summary>
    [MaxLength(500)]
    public string? RawLinkText { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
