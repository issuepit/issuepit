using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Notes.Core.Entities;

/// <summary>
/// Many-to-many mapping between notes and tags.
/// </summary>
[Table("note_tag_mappings")]
public class NoteTagMapping
{
    public Guid NoteId { get; set; }

    [ForeignKey(nameof(NoteId))]
    public Note Note { get; set; } = null!;

    public Guid TagId { get; set; }

    [ForeignKey(nameof(TagId))]
    public NoteTag Tag { get; set; } = null!;
}
