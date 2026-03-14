using System.IO.Compression;
using System.Text;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.CiCdClient.Runtimes;

/// <summary>
/// Dry-run (simulation) CI/CD runtime. Emits a scripted sequence of log lines without
/// actually invoking <c>act</c>. Only active when the configuration key <c>CiCd:DryRun</c>
/// is <c>true</c> (environment variable: <c>CiCd__DryRun=true</c>).
/// Emits act-compatible JSON log lines so the worker can parse job and level info.
/// Also writes fake artifacts matching the dummy-cicd-repo workflow into
/// <see cref="TriggerPayload.ArtifactServerPath"/> (when set) so downstream artifact
/// collection, TRX parsing, and S3 upload can be exercised end-to-end in tests.
/// </summary>
public class DryRunCiCdRuntime(ILogger<DryRunCiCdRuntime> logger) : ICiCdRuntime
{
    // Minimal TRX matching the dummy-cicd-repo workflow so E2E tests can assert on parsed results.
    private const string DummyTrxContent = """
        <?xml version="1.0" encoding="UTF-8"?>
        <TestRun id="1" name="DummyRun" start="2024-01-01T10:00:00" finish="2024-01-01T10:00:05" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
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

        // Write fake artifacts into ArtifactServerPath so the post-run artifact collection,
        // TRX parsing, and S3 upload can be exercised in dry-run / E2E test mode.
        // act's artifact server uses the layout: <artifactServerPath>/<runNumber>/<name>/<file>.zip
        // where each uploaded file is stored as an individual ZIP archive.
        if (!string.IsNullOrWhiteSpace(trigger.ArtifactServerPath))
        {
            try
            {
                // build-output artifact: a plain text file (packed as a zip, matching act's format)
                var buildOutputDir = Path.Combine(trigger.ArtifactServerPath, "1", "build-output");
                Directory.CreateDirectory(buildOutputDir);
                WriteArtifactZip(
                    Path.Combine(buildOutputDir, "build-output.txt.zip"),
                    "build-output.txt",
                    $"Build succeeded (dry-run for CI/CD run {run.Id})\n");

                // test-results artifact: a minimal TRX file matching the dummy-cicd-repo workflow
                var testResultsDir = Path.Combine(trigger.ArtifactServerPath, "1", "test-results");
                Directory.CreateDirectory(testResultsDir);
                WriteArtifactZip(
                    Path.Combine(testResultsDir, "test-results.trx.zip"),
                    "test-results.trx",
                    DummyTrxContent);

                logger.LogDebug("Wrote fake artifacts for dry-run CI/CD run {RunId}", run.Id);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not write fake artifacts for dry-run CI/CD run {RunId}", run.Id);
            }
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

    /// <summary>
    /// Creates a ZIP archive at <paramref name="zipPath"/> containing a single UTF-8 encoded
    /// entry, matching the format used by the act artifact server for each uploaded file.
    /// </summary>
    private static void WriteArtifactZip(string zipPath, string entryName, string content)
    {
        using var fs = File.Create(zipPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);
        var entry = archive.CreateEntry(entryName);
        using var entryStream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        entryStream.Write(bytes, 0, bytes.Length);
    }
}
