using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.ExecutionClient.Runtimes;

public interface IAgentRuntime
{
    /// <summary>Launches the agent for the given session and returns a runtime-specific identifier (e.g., container ID).</summary>
    /// <param name="gitRepository">Push target — always the Working-mode remote.</param>
    /// <param name="cloneRepository">
    ///   Clone source — the remote with the most recently confirmed content (highest <c>LastFetchedAt</c>).
    ///   When <c>null</c>, <paramref name="gitRepository"/> is used for both clone and push.
    /// </param>
    Task<string> LaunchAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        RuntimeConfiguration? runtimeConfig,
        GitRepository? gitRepository,
        GitRepository? cloneRepository,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken);
}
