namespace IssuePit.Notes.Core.Enums;

/// <summary>
/// Backend storage provider for a notebook's notes.
/// </summary>
public enum StorageProvider
{
    Postgres,
    Sqlite,
    Git,
    // S3 — reserved, not yet implemented
    // Elasticsearch — reserved, not yet implemented
}
