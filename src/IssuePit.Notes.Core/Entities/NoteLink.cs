using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Notes.Core.Enums;

namespace IssuePit.Notes.Core.Entities;

/// <summary>
/// A directional link from one note to another entity (note, project, issue, or todo).
/// Parsed from [[...]] wiki-style syntax in markdown content.
/// </summary>
[Table("note_links")]
public class NoteLink
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The note this link originates from.
    /// </summary>
    public Guid SourceNoteId { get; set; }

    [ForeignKey(nameof(SourceNoteId))]
    public Note SourceNote { get; set; } = null!;

    /// <summary>
    /// The type of entity being linked to.
    /// </summary>
    public NoteLinkType TargetType { get; set; }

    /// <summary>
    /// When TargetType is Note, the ID of the target note.
    /// </summary>
    public Guid? TargetNoteId { get; set; }

    [ForeignKey(nameof(TargetNoteId))]
    public Note? TargetNote { get; set; }

    /// <summary>
    /// For external entity links (project, issue, todo), the target entity's ID.
    /// </summary>
    public Guid? TargetEntityId { get; set; }

    /// <summary>
    /// The original link text from the markdown source (e.g. "My Other Note" from [[My Other Note]]).
    /// </summary>
    [MaxLength(500)]
    public string LinkText { get; set; } = string.Empty;
}
