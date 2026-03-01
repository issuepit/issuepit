using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.CiCdClient.Runtimes;

/// <summary>
/// Abstraction for the CI/CD workflow execution engine.
/// Mirrors the <c>IAgentRuntime</c> pattern used by the ExecutionClient.
/// </summary>
public interface ICiCdRuntime
{
    /// <summary>
    /// Runs the CI/CD workflow described by <paramref name="trigger"/> for the given <paramref name="run"/>,
    /// invoking <paramref name="onLogLine"/> for each line of output produced.
    /// </summary>
    Task RunAsync(
        CiCdRun run,
        TriggerPayload trigger,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken);
}
