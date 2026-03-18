namespace IssuePit.Core.Enums;

/// <summary>
/// Controls whether and how agents are permitted to run <c>git push</c> commands
/// directly inside the agent container (i.e. during the agent's working session,
/// before the execution client performs its own managed push).
/// </summary>
public enum AgentPushRestriction
{
    /// <summary>
    /// Agents are never allowed to push directly. The execution client performs the
    /// only push after the agent session ends. This is the default and safest option.
    /// </summary>
    Forbidden = 0,

    /// <summary>
    /// Agents may push, but only to the feature branch created for the current run.
    /// Force pushes and pushes to the default branch (main/master) are always denied.
    /// Use this on Working-mode origins where you want incremental pushes during the session.
    /// </summary>
    WorkingOriginOnly = 1,

    /// <summary>
    /// Agents may push to any non-protected branch.
    /// Force pushes and pushes to the default branch (main/master) are always denied.
    /// </summary>
    Allowed = 2,

    /// <summary>
    /// No restrictions enforced by the wrapper. Agents may push to any branch.
    /// Force pushes to the default branch are still denied as a last safety net.
    /// Use with caution.
    /// </summary>
    YoloMode = 3,
}
