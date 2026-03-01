using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.CiCdClient.Runtimes;

/// <summary>
/// Dry-run (simulation) CI/CD runtime. Emits a scripted sequence of log lines without
/// actually invoking <c>act</c>. Only active when <c>CiCd__DryRun=true</c>.
/// </summary>
public class DryRunCiCdRuntime(ILogger<DryRunCiCdRuntime> logger) : ICiCdRuntime
{
    public async Task RunAsync(
        CiCdRun run,
        TriggerPayload trigger,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Dry-run mode active for CI/CD run {RunId}", run.Id);

        var lines = new[]
        {
            $"[INFO] Starting workflow '{trigger.Workflow ?? "default"}' for commit {trigger.CommitSha}",
            "[INFO] Pulling runner image…",
            "[INFO] Running job: build",
            "[INFO] ✓ Restore succeeded",
            "[INFO] ✓ Build succeeded",
            "[INFO] Running job: test",
            "[INFO] ✓ Tests passed",
            "[INFO] Workflow completed successfully",
        };

        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await onLogLine(line, LogStream.Stdout);
            await Task.Delay(200, cancellationToken);
        }
    }
}
