using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.CiCdClient.Runtimes;

/// <summary>
/// Dry-run (simulation) CI/CD runtime. Emits a scripted sequence of log lines without
/// actually invoking <c>act</c>. Only active when <c>CiCd__DryRun=true</c>.
/// Emits act-compatible JSON log lines so the worker can parse job and level info.
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
            ActJson("info", $"Starting workflow '{trigger.Workflow ?? "default"}' for commit {trigger.CommitSha}", null),
            ActJson("info", "Pulling runner image\u2026", null),
            ActJson("info", "Set up job", "build"),
            ActJson("info", "\u2713 Restore succeeded", "build"),
            ActJson("info", "\u2713 Build succeeded", "build"),
            ActJson("info", "Job succeeded", "build"),
            ActJson("info", "Set up job", "test"),
            ActJson("info", "\u2713 Tests passed", "test"),
            ActJson("info", "Job succeeded", "test"),
            ActJson("info", "Workflow completed successfully", null),
        };

        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await onLogLine(line, LogStream.Stdout);
            await Task.Delay(200, cancellationToken);
        }
    }

    /// <summary>Builds an act-compatible JSON log line (logrus format).</summary>
    private static string ActJson(string level, string msg, string? job)
    {
        var time = DateTime.UtcNow.ToString("o");
        if (job is not null)
            return $"{{\"level\":\"{level}\",\"msg\":{System.Text.Json.JsonSerializer.Serialize(msg)},\"job\":\"{job}\",\"time\":\"{time}\"}}";
        return $"{{\"level\":\"{level}\",\"msg\":{System.Text.Json.JsonSerializer.Serialize(msg)},\"time\":\"{time}\"}}";
    }
}
