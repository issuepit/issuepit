using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>
/// An optional extension of <see cref="IAgentRuntime"/> for runtimes that keep the agent
/// container alive after the initial run and support executing further commands inside the
/// same container via docker exec.
///
/// This enables fix runs (uncommitted-changes or CI/CD failures) to reuse the container that
/// the initial agent run created, so that:
///   - The git workspace already contains the changes from the first run.
///   - opencode session files are on disk, allowing <c>opencode run --fork &lt;session-id&gt;</c>
///     to resume with full conversation context once opencode supports the --fork flag.
/// </summary>
public interface IExecCapableRuntime : IAgentRuntime
{
    /// <summary>
    /// Runs a fix task inside an already-running container.
    /// Streams output to <paramref name="onLogLine"/> (with a <c>[fix]</c> prefix) and emits
    /// <c>[ISSUEPIT:GIT_COMMIT_SHA]</c> / <c>[ISSUEPIT:GIT_BRANCH]</c> markers after the run.
    /// Returns the updated (commitSha, branchName), or (null, null) if git info is unavailable.
    /// </summary>
    /// <param name="containerId">ID of the running container (returned by <see cref="IAgentRuntime.LaunchAsync"/>).</param>
    /// <param name="openCodeSessionId">
    /// The opencode session ID captured during the initial run.
    /// Will be passed as <c>--fork &lt;id&gt;</c> once opencode supports that flag in non-interactive mode.
    /// </param>
    Task<(string? CommitSha, string? BranchName)> ExecFixInContainerAsync(
        string containerId,
        string? openCodeSessionId,
        AgentSession parentSession,
        Agent agent,
        Issue fixIssue,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stops and optionally removes the container when all work is complete.
    /// When <paramref name="remove"/> is <c>false</c> (i.e. <c>KeepContainer=true</c>) the container
    /// is stopped but left on disk for developer inspection.
    /// </summary>
    Task StopContainerAsync(string containerId, bool remove, CancellationToken cancellationToken);
}
