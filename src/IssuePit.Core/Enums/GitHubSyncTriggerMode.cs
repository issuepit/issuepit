namespace IssuePit.Core.Enums;

/// <summary>Controls when automatic GitHub synchronisation is triggered for a project.</summary>
public enum GitHubSyncTriggerMode
{
    /// <summary>Sync is disabled. No automatic or background syncing occurs.</summary>
    Off = 0,

    /// <summary>Sync must be manually triggered from the UI or API.</summary>
    Manual = 1,

    /// <summary>Sync runs automatically on a schedule. Not the default — enable explicitly.</summary>
    Auto = 2,
}
