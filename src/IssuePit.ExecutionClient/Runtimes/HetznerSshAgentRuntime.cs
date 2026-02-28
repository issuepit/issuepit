using IssuePit.Core.Entities;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>Provisions a Hetzner Cloud server via Terraform and runs the agent over SSH.</summary>
public class HetznerSshAgentRuntime(ILogger<HetznerSshAgentRuntime> logger) : IAgentRuntime
{
    public Task<string> LaunchAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "HetznerSshAgentRuntime is not yet implemented. Session {SessionId} skipped.", session.Id);
        return Task.FromResult(string.Empty);
    }
}
