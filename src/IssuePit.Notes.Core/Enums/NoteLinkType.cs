namespace IssuePit.Notes.Core.Enums;

/// <summary>
/// Types of links that can exist between notes and other entities.
/// </summary>
public enum NoteLinkType
{
    /// <summary>Wiki-style link between two notes ([[note-title]]).</summary>
    NoteToNote = 0,

    /// <summary>Link from a note to an IssuePit project.</summary>
    NoteToProject = 1,

    /// <summary>Link from a note to an IssuePit issue.</summary>
    NoteToIssue = 2,

    /// <summary>Link from a note to an IssuePit todo.</summary>
    NoteToTodo = 3
}
