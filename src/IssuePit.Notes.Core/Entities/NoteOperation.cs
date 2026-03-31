using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Notes.Core.Entities;

/// <summary>
/// Stores an OT (Operational Transformation) operation in the CRDT event log for a note.
/// Each operation represents an atomic content change submitted by a client.
/// The server assigns a monotonically increasing <see cref="SequenceNumber"/> per note
/// and transforms concurrent operations before applying them, ensuring all clients converge
/// to the same document state regardless of the order operations were received.
/// </summary>
[Table("note_operations")]
public class NoteOperation
{
    [Key]
    public Guid Id { get; set; }

    public Guid NoteId { get; set; }

    [ForeignKey(nameof(NoteId))]
    public Note Note { get; set; } = null!;

    /// <summary>
    /// Identifies the editing session that submitted this operation (UUID generated per browser tab).
    /// Used for echo-suppression: clients skip applying their own confirmed operations.
    /// </summary>
    [MaxLength(100)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Server-assigned monotonically increasing sequence number per note.
    /// Clients use this to fetch only operations they haven't seen yet.
    /// </summary>
    public long SequenceNumber { get; set; }

    /// <summary>
    /// The note version the client had when computing this delta.
    /// The server transforms this delta against all operations with
    /// SequenceNumber &gt; BaseSequence before applying.
    /// </summary>
    public long BaseSequence { get; set; }

    /// <summary>
    /// JSON-serialized list of OT operations in Quill delta format:
    /// [{"retain":N}, {"insert":"text"}, {"delete":N}]
    /// Operations are applied to the raw plain-text representation of the note content.
    /// </summary>
    public string Delta { get; set; } = string.Empty;

    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// True when this row is a compacted summary of multiple same-day operations.
    /// Compaction runs nightly and merges ops older than 30 days into one per day.
    /// </summary>
    public bool IsCompacted { get; set; }
}
