namespace IssuePit.Notes.Core.Enums;

/// <summary>
/// Storage engine used by a note workspace.
/// Extensible: only Postgres is implemented initially; others are prepared for future use.
/// </summary>
public enum NoteStorageEngine
{
    /// <summary>Default: notes stored in the Notes PostgreSQL database.</summary>
    Postgres = 0,

    /// <summary>Notes stored in a SQLite file (prepared, not yet implemented).</summary>
    Sqlite = 1,

    /// <summary>Notes synced to a Git repository (prepared, not yet implemented).</summary>
    Git = 2,

    /// <summary>Notes stored in S3-compatible storage (prepared, not yet implemented).</summary>
    S3 = 3,

    /// <summary>Notes indexed in Elasticsearch/OpenSearch (prepared, not yet implemented).</summary>
    Elasticsearch = 4
}
