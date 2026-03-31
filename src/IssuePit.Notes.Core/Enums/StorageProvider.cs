namespace IssuePit.Notes.Core.Enums;

/// <summary>
/// Backend storage provider for a notebook's notes.
/// S3 and Elasticsearch are planned but not yet implemented. Their enum values are reserved
/// to prevent accidental value collisions when they are added later.
/// </summary>
public enum StorageProvider
{
    Postgres,
    Sqlite,
    Git,
    S3,            // reserved — not yet implemented
    Elasticsearch, // reserved — not yet implemented
}
