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
    /// <summary>
    /// The run completed successfully but with non-fatal warnings (e.g. the cloned commit SHA did
    /// not match the requested trigger SHA because the branch advanced between trigger and clone).
    /// </summary>
    SucceededWithWarnings,
}
