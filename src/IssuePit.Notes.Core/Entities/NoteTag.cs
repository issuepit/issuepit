using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Notes.Core.Entities;

[Table("note_tags")]
public class NoteTag
{
    [Key]
    public Guid Id { get; set; }

    public Guid NotebookId { get; set; }

    [ForeignKey(nameof(NotebookId))]
    public Notebook Notebook { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Color { get; set; } = "#6b7280";

    public ICollection<NoteTagMapping> NoteMappings { get; set; } = [];
}
