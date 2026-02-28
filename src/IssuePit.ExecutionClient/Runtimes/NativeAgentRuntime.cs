using IssuePit.Core.Entities;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>Runs the agent directly as a process on the host machine (bare-metal).</summary>
public class NativeAgentRuntime(ILogger<NativeAgentRuntime> logger) : IAgentRuntime
{
    public Task<string> LaunchAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "NativeAgentRuntime is not yet implemented. Session {SessionId} skipped.", session.Id);
        return Task.FromResult(string.Empty);
    }
}
