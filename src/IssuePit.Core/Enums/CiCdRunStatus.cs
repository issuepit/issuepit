namespace IssuePit.Core.Enums;

public enum CiCdRunStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    /// <summary>
    /// The run is waiting for explicit approval before it will be dispatched to the CI/CD worker.
    /// Set when the project has <see cref="IssuePit.Core.Entities.Project.RequiresRunApproval"/> = true.
    /// </summary>
    WaitingForApproval,
}
