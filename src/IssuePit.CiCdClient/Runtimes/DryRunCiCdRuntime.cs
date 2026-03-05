using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.CiCdClient.Runtimes;

/// <summary>
/// Dry-run (simulation) CI/CD runtime. Emits a scripted sequence of log lines without
/// actually invoking <c>act</c>. Only active when <c>CiCd__DryRun=true</c>.
/// Emits act-compatible JSON log lines so the worker can parse job and level info.
/// Also writes a dummy artifact directory and a minimal TRX test-results file to
/// <see cref="TriggerPayload.ArtifactServerPath"/> so E2E tests can verify artifact
/// and test-result persistence without Docker or act.
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

        // Write a dummy artifact and a minimal TRX file to the artifact server path so the
        // CiCdWorker can store artifact metadata and test results from the dry-run.
        // This mirrors what act + actions/upload-artifact would produce in a real run.
        if (!string.IsNullOrWhiteSpace(trigger.ArtifactServerPath))
        {
            WriteDummyArtifact(trigger.ArtifactServerPath, run.Id);
            WriteDummyTrxFile(trigger.ArtifactServerPath, run.Id);
        }
    }

    /// <summary>
    /// Writes a dummy artifact directory with a single placeholder file under
    /// <c>&lt;artifactServerPath&gt;/dry-run-artifact/</c>.
    /// Best-effort: errors are swallowed so a file-system hiccup does not fail the run.
    /// </summary>
    private static void WriteDummyArtifact(string artifactServerPath, Guid runId)
    {
        try
        {
            var artifactDir = Path.Combine(artifactServerPath, "dry-run-artifact");
            Directory.CreateDirectory(artifactDir);
            File.WriteAllText(
                Path.Combine(artifactDir, "output.txt"),
                $"Dry-run artifact produced by run {runId}.\n");
        }
        catch
        {
            // best-effort: do not fail the run if artifact write fails
        }
    }

    /// <summary>
    /// Writes a minimal TRX file with two passing test cases under
    /// <c>&lt;artifactServerPath&gt;/test-results/</c>.
    /// Best-effort: errors are swallowed so a file-system hiccup does not fail the run.
    /// </summary>
    private static void WriteDummyTrxFile(string artifactServerPath, Guid runId)
    {
        try
        {
            var resultsDir = Path.Combine(artifactServerPath, "test-results");
            Directory.CreateDirectory(resultsDir);

            var trxPath = Path.Combine(resultsDir, "dry-run-results.trx");
            var now = DateTime.UtcNow;
            var start = now.ToString("o");
            var finish = now.AddSeconds(1).ToString("o");
            var testId1 = Guid.NewGuid().ToString();
            var testId2 = Guid.NewGuid().ToString();

            var trxContent = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <TestRun id="{runId}" name="DryRunTestRun" start="{start}" finish="{finish}" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                  <TestDefinitions>
                    <UnitTest name="DryRunTest_Build" id="{testId1}">
                      <TestMethod className="DryRun.Tests.BuildTests" name="DryRunTest_Build" />
                    </UnitTest>
                    <UnitTest name="DryRunTest_Integration" id="{testId2}">
                      <TestMethod className="DryRun.Tests.IntegrationTests" name="DryRunTest_Integration" />
                    </UnitTest>
                  </TestDefinitions>
                  <Results>
                    <UnitTestResult testId="{testId1}" testName="DryRunTest_Build" outcome="Passed" duration="00:00:00.0500000" />
                    <UnitTestResult testId="{testId2}" testName="DryRunTest_Integration" outcome="Passed" duration="00:00:00.1000000" />
                  </Results>
                  <ResultSummary outcome="Completed">
                    <Counters total="2" executed="2" passed="2" failed="0" error="0" />
                  </ResultSummary>
                </TestRun>
                """;

            File.WriteAllText(trxPath, trxContent);
        }
        catch
        {
            // best-effort: do not fail the run if TRX file write fails
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
