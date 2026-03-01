using IssuePit.Core.Entities;

namespace IssuePit.ExecutionClient.Runtimes;

public interface IAgentRuntime
{
    /// <summary>Launches the agent for the given session and returns a runtime-specific identifier (e.g., container ID).</summary>
    Task<string> LaunchAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        RuntimeConfiguration? runtimeConfig,
        GitRepository? gitRepository,
        CancellationToken cancellationToken);
}
