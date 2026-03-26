namespace IssuePit.Core.Enums;

/// <summary>Controls when automatic Jira sync is triggered for a project.</summary>
public enum JiraSyncTriggerMode
{
    /// <summary>Sync is disabled. No automatic or manual runs.</summary>
    Off = 0,

    /// <summary>Sync can only be triggered manually from the Jira Sync page.</summary>
    Manual = 1,

    /// <summary>Sync runs automatically on a schedule (in addition to manual triggers).</summary>
    Auto = 2,
}
