using IssuePit.Core.Entities;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>Uses Alibaba OpenSandbox as the agent execution environment.</summary>
public class OpenSandboxAgentRuntime(ILogger<OpenSandboxAgentRuntime> logger) : IAgentRuntime
{
    public Task<string> LaunchAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "OpenSandboxAgentRuntime is not yet implemented. Session {SessionId} skipped.", session.Id);
        return Task.FromResult(string.Empty);
    }
}
