namespace IssuePit.Core.Enums;

/// <summary>
/// Controls whether and under what conditions the execution client may push the agent's
/// working branch to a remote git repository after the agent session completes.
/// 
/// Regardless of the policy, the following are always denied:
/// <list type="bullet">
///   <item>Force pushes (only plain pushes are ever issued).</item>
///   <item>Pushes to the repository's default branch (main, master, or the configured default).</item>
/// </list>
/// </summary>
public enum AgentPushPolicy
{
    /// <summary>
    /// No push is performed after the agent session ends.
    /// The agent's committed work remains local to the container and is not pushed to any remote.
    /// This is the default.
    /// </summary>
    Forbidden = 0,

    /// <summary>
    /// Push is only performed when the target repository has mode <see cref="GitOriginMode.Working"/>.
    /// Repos with mode <see cref="GitOriginMode.ReadOnly"/> or <see cref="GitOriginMode.Release"/>
    /// are skipped.
    /// </summary>
    WorkingOriginOnly = 1,

    /// <summary>
    /// Push is performed for any repository except those with mode <see cref="GitOriginMode.ReadOnly"/>.
    /// </summary>
    Allowed = 2,

    /// <summary>
    /// Push is performed unconditionally, regardless of the repository mode.
    /// Basic safety guards (no force-push, no push to default branch) still apply.
    /// </summary>
    Yolo = 3,
}
