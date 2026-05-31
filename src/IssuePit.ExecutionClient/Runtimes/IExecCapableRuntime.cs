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
    /// Returns the updated (commitSha, branchName, newSessionId), or (null, null, null) if git info or
    /// session ID is unavailable.
    /// </summary>
    /// <param name="containerId">ID of the running container (returned by <see cref="IAgentRuntime.LaunchAsync"/>).</param>
    /// <param name="openCodeSessionId">
    /// The opencode session ID to pass to <c>opencode run</c>.
    /// When <paramref name="fork"/> is <c>true</c>, passed as <c>--session &lt;id&gt; --fork</c> to
    /// create a child branch of the session with full conversation context.
    /// When <paramref name="fork"/> is <c>false</c>, passed as <c>--session &lt;id&gt;</c> to continue
    /// an already-forked session without creating a new branch.
    /// See https://opencode.ai/docs/cli/#run-1.
    /// </param>
    /// <param name="fork">
    /// When <c>true</c>, builds the opencode command with <c>--fork</c> so the run starts a child branch
    /// of <paramref name="openCodeSessionId"/>. When <c>false</c>, continues in the existing session
    /// without forking. Use <c>true</c> for the first CI/CD fix run and <c>false</c> for subsequent runs
    /// so that all CI/CD fixes build on the same forked session rather than creating new branches of the
    /// main agent session each time.
    /// </param>
    Task<(string? CommitSha, string? BranchName, string? NewSessionId)> ExecFixInContainerAsync(
        string containerId,
        string? openCodeSessionId,
        bool fork,
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

    /// <summary>
    /// Runs a dedicated opencode session to analyse raw CI/CD logs and produce a condensed
    /// failure report. The full log text is written to a file inside the container; a new
    /// (non-forked) opencode session analyses it and writes a summary to a second file.
    /// Returns the condensed report text, or <c>null</c> if the condensing session failed.
    /// </summary>
    Task<string?> CondenseLogsInContainerAsync(
        string containerId,
        string rawLogs,
        Agent agent,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken);

    /// <summary>
    /// After a failed push, loops through <paramref name="allGitRepositories"/> in order, fetching
    /// and rebasing the local branch on top of each remote's version of the branch. Once all remotes
    /// are integrated, retries the push to <paramref name="gitRepository"/> (the Working push target).
    /// Emits an updated <c>[ISSUEPIT:GIT_COMMIT_SHA]</c> marker when the retry succeeds.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the retry push succeeded after integrating all reachable remotes;
    /// <c>false</c> if any fetch failed (non-fatal — branch not on remote), any rebase produced
    /// conflicts (fatal — abort is attempted), or the retry push itself failed.
    /// </returns>
    Task<bool> TryIntegrateRemotesAndRetryPushAsync(
        string containerId,
        GitRepository gitRepository,
        IReadOnlyList<GitRepository> allGitRepositories,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken);
}
