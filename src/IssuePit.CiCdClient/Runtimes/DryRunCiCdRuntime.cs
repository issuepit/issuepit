using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.CiCdClient.Runtimes;

/// <summary>
/// Dry-run (simulation) CI/CD runtime. Emits a scripted sequence of log lines without
/// actually invoking <c>act</c>. Only active when <c>CiCd:DryRun=true</c> (env: <c>CiCd__DryRun=true</c>).
/// Emits act-compatible JSON log lines so the worker can parse job and level info.
///
/// When <see cref="TriggerPayload.ArtifactServerPath"/> is set the runtime also writes
/// simulated artifact files so the CiCdWorker's artifact- and TRX-parsing pipeline can be
/// exercised in the same dry-run pass.
/// </summary>
public class DryRunCiCdRuntime(ILogger<DryRunCiCdRuntime> logger) : ICiCdRuntime
{
    // Minimal valid TRX produced by the dry-run test job.
    internal const string DryRunTrxContent = """
        <?xml version="1.0" encoding="UTF-8"?>
        <TestRun id="dry-run-1" name="DryRun" start="2024-01-01T10:00:00" finish="2024-01-01T10:00:05" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <TestDefinitions>
            <UnitTest name="DummyTest_Passes" id="test-1">
              <TestMethod className="DummyProject.DummyTests" name="DummyTest_Passes" />
            </UnitTest>
          </TestDefinitions>
          <Results>
            <UnitTestResult testId="test-1" testName="DummyTest_Passes" outcome="Passed" duration="00:00:00.1000000" />
          </Results>
          <ResultSummary outcome="Completed">
            <Counters total="1" executed="1" passed="1" failed="0" error="0" />
          </ResultSummary>
        </TestRun>
        """;

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

        // Write simulated artifacts so the worker's artifact- and TRX-parsing pipeline is
        // exercised even when no real act/Docker execution takes place.
        WriteSimulatedArtifacts(trigger.ArtifactServerPath);
    }

    /// <summary>
    /// Creates simulated artifact files under <paramref name="artifactServerPath"/>:
    /// <list type="bullet">
    ///   <item><c>build-output/1/output.txt</c> – dummy build artifact.</item>
    ///   <item><c>test-results/1/results.trx</c> – minimal TRX file parsed by <c>TrxParser</c>.</item>
    /// </list>
    /// Follows the act artifact-server layout (<c>&lt;name&gt;/&lt;runNumber&gt;/&lt;files&gt;</c>).
    /// No-op when <paramref name="artifactServerPath"/> is null or empty.
    /// </summary>
    internal static void WriteSimulatedArtifacts(string? artifactServerPath)
    {
        if (string.IsNullOrWhiteSpace(artifactServerPath)) return;

        try
        {
            // build-output artifact
            var buildDir = Path.Combine(artifactServerPath, "build-output", "1");
            Directory.CreateDirectory(buildDir);
            File.WriteAllText(Path.Combine(buildDir, "output.txt"), "Build succeeded (dry-run)");

            // test-results artifact containing a valid TRX file
            var testResultsDir = Path.Combine(artifactServerPath, "test-results", "1");
            Directory.CreateDirectory(testResultsDir);
            File.WriteAllText(Path.Combine(testResultsDir, "results.trx"), DryRunTrxContent);
        }
        catch
        {
            // Best-effort — never throw from a dry-run simulation.
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
