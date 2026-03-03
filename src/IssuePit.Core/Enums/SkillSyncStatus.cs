namespace IssuePit.Core.Enums;

public enum SkillSyncStatus
{
    /// <summary>No git repository configured for this skill.</summary>
    None = 0,

    /// <summary>Local content is in sync with the remote git repository.</summary>
    Synced = 1,

    /// <summary>Local content has unpushed changes ahead of the remote.</summary>
    Ahead = 2,

    /// <summary>Remote repository has new commits not yet pulled locally.</summary>
    Behind = 3,

    /// <summary>An error occurred during the last sync attempt.</summary>
    Error = 4,
}
