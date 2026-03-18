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
///   - <c>opencode run --session &lt;id&gt; --fork</c> resumes with full conversation context
///     from the session that made the original changes.
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
    /// Passed as <c>--session &lt;id&gt; --fork</c> so the fix run continues the same session
    /// with full conversation context. See https://opencode.ai/docs/cli/#run-1.
    /// </param>
    Task<(string? CommitSha, string? BranchName)> ExecFixInContainerAsync(
        string containerId,
        string? openCodeSessionId,
        AgentSession parentSession,
        Agent agent,
        Issue fixIssue,
        GitRepository? gitRepository,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stops and optionally removes the container when all work is complete.
    /// When <paramref name="remove"/> is <c>false</c> (i.e. <c>KeepContainer=true</c>) the container
    /// is stopped but left on disk for developer inspection.
    /// </summary>
    Task StopContainerAsync(string containerId, bool remove, CancellationToken cancellationToken);
}
