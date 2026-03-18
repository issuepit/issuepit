namespace IssuePit.Core.Entities;

/// <summary>
/// Captures the result of a pre-flight <c>git ls-remote</c> branch check against a single
/// configured git remote. A list of these is serialised to JSON and stored on
/// <see cref="AgentSession.GitRemoteCheckResultsJson"/> so the UI can show which remotes had
/// the branch available at the time of the run.
/// </summary>
/// <param name="RepoId">The <see cref="GitRepository.Id"/> of the remote that was checked.</param>
/// <param name="RemoteUrl">The remote URL (without embedded credentials).</param>
/// <param name="Mode">The <see cref="IssuePit.Core.Enums.GitOriginMode"/> of the repo as a string
/// (e.g. <c>"Working"</c>, <c>"Release"</c>, <c>"ReadOnly"</c>).</param>
/// <param name="DefaultBranch">The configured default branch that was checked (null/empty when not configured).</param>
/// <param name="Available">
/// <c>true</c> if the branch was found on the remote;
/// <c>false</c> if the branch was not found;
/// <c>null</c> if the check was skipped (git not available on host, timeout, network error).
/// </param>
/// <param name="Selected">Whether this remote was selected as the clone target for the session.</param>
public record GitRemoteCheckResult(
    Guid RepoId,
    string RemoteUrl,
    string Mode,
    string? DefaultBranch,
    bool? Available,
    bool Selected = false);
