namespace IssuePit.Core.Enums;

/// <summary>Represents the lifecycle status of a single GitHub sync run.</summary>
public enum GitHubSyncRunStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
}
