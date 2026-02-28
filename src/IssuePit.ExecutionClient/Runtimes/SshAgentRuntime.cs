using IssuePit.Core.Entities;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>Connects to a remote host via SSH and runs the agent there.</summary>
public class SshAgentRuntime(ILogger<SshAgentRuntime> logger) : IAgentRuntime
{
    public Task<string> LaunchAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "SshAgentRuntime is not yet implemented. Session {SessionId} skipped.", session.Id);
        return Task.FromResult(string.Empty);
    }
}
