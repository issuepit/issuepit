using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.Core.Services;

namespace IssuePit.TestRunner.Services;

/// <summary>
/// Service that discovers and executes .NET tests by invoking <c>dotnet test</c> as an external
/// process. Test results are captured via TRX output and parsed using <see cref="TrxParser"/>.
/// </summary>
public sealed class DotNetTestRunnerService
{
    private readonly ILogger<DotNetTestRunnerService> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>In-memory store of all test runs keyed by run ID.</summary>
    private readonly ConcurrentDictionary<Guid, TestRunInfo> _runs = new();

    public DotNetTestRunnerService(ILogger<DotNetTestRunnerService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Discovers available tests from the configured solution or project path by invoking
    /// <c>dotnet test --list-tests</c>.
    /// </summary>
    public async Task<TestDiscoveryResult> DiscoverTestsAsync(CancellationToken cancellationToken = default)
    {
        var solutionPath = ResolveSolutionPath();
        if (solutionPath is null)
            return new TestDiscoveryResult([], "Solution path not configured or not found.");

        var args = $"test \"{solutionPath}\" --list-tests --verbosity quiet";
        _logger.LogInformation("Discovering tests: dotnet {Args}", args);

        var (exitCode, output) = await RunDotNetAsync(args, TimeSpan.FromMinutes(3), cancellationToken);

        if (exitCode != 0)
        {
            _logger.LogWarning("Test discovery exited with code {ExitCode}", exitCode);
            return new TestDiscoveryResult([], $"dotnet test --list-tests exited with code {exitCode}.\n{output}");
        }

        // Parse the output: lines after "The following Tests are available:" are test names.
        var tests = new List<string>();
        var capturing = false;
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("The following Tests are available:", StringComparison.OrdinalIgnoreCase))
            {
                capturing = true;
                continue;
            }
            if (capturing && !string.IsNullOrWhiteSpace(trimmed))
                tests.Add(trimmed);
        }

        return new TestDiscoveryResult(tests, null);
    }

    /// <summary>
    /// Starts a new test run. The tests are executed asynchronously; callers can poll the run
    /// status via <see cref="GetRunAsync"/>.
    /// </summary>
    /// <param name="filter">Optional dotnet test filter expression (e.g. <c>FullyQualifiedName~MyTest</c>).</param>
    /// <param name="project">Optional path to a specific test project; defaults to the configured solution.</param>
    public TestRunInfo StartRun(string? filter = null, string? project = null)
    {
        var targetPath = project ?? ResolveSolutionPath()
            ?? throw new InvalidOperationException("Solution path not configured or not found.");

        var run = new TestRunInfo
        {
            Id = Guid.NewGuid(),
            Status = TestRunStatus.Pending,
            Filter = filter,
            ProjectPath = targetPath,
            StartedAt = DateTime.UtcNow,
        };

        _runs[run.Id] = run;

        // Fire-and-forget the actual test execution.
        _ = Task.Run(() => ExecuteRunAsync(run));

        return run;
    }

    /// <summary>Returns a snapshot of the specified run, or <c>null</c> if not found.</summary>
    public TestRunInfo? GetRun(Guid runId) => _runs.TryGetValue(runId, out var run) ? run : null;

    /// <summary>Returns all runs ordered by start time descending.</summary>
    public IReadOnlyList<TestRunInfo> ListRuns() =>
        _runs.Values.OrderByDescending(r => r.StartedAt).ToList();

    private async Task ExecuteRunAsync(TestRunInfo run)
    {
        var trxDir = Path.Combine(Path.GetTempPath(), "issuepit-test-runner", run.Id.ToString());
        Directory.CreateDirectory(trxDir);

        try
        {
            run.Status = TestRunStatus.Running;

            var argsBuilder = new StringBuilder();
            // Use LogFilePrefix instead of LogFileName so each test project gets a unique
            // TRX file. When running against a solution, dotnet test invokes each project
            // sequentially and a fixed LogFileName would overwrite previous results.
            argsBuilder.Append($"test \"{run.ProjectPath}\" --logger \"trx;LogFilePrefix=results\" --results-directory \"{trxDir}\"");

            if (!string.IsNullOrWhiteSpace(run.Filter))
                argsBuilder.Append($" --filter \"{run.Filter}\"");

            var args = argsBuilder.ToString();
            _logger.LogInformation("Starting test run {RunId}: dotnet {Args}", run.Id, args);

            var (exitCode, output) = await RunDotNetAsync(args, TimeSpan.FromMinutes(30), CancellationToken.None);

            run.Output = output;
            run.ExitCode = exitCode;

            // Parse all TRX results and merge into a single aggregate suite.
            var trxFiles = TrxParser.FindTrxFiles(trxDir).ToList();
            CiCdTestSuite? merged = null;
            foreach (var trxFile in trxFiles)
            {
                var suite = TrxParser.Parse(trxFile);
                if (suite is null) continue;
                if (merged is null)
                {
                    merged = suite;
                }
                else
                {
                    merged.TotalTests += suite.TotalTests;
                    merged.PassedTests += suite.PassedTests;
                    merged.FailedTests += suite.FailedTests;
                    merged.SkippedTests += suite.SkippedTests;
                    merged.DurationMs += suite.DurationMs;
                    foreach (var tc in suite.TestCases)
                        merged.TestCases.Add(tc);
                }
            }
            if (merged is not null)
                run.Suite = merged;

            run.Status = exitCode == 0 ? TestRunStatus.Completed : TestRunStatus.Failed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test run {RunId} failed with exception", run.Id);
            run.Output += $"\n\nException: {ex.Message}";
            run.Status = TestRunStatus.Failed;
        }
        finally
        {
            run.FinishedAt = DateTime.UtcNow;

            // Clean up TRX temp directory.
            try { Directory.Delete(trxDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    private string? ResolveSolutionPath()
    {
        // 1) Explicit configuration
        var configured = _configuration["TestRunner:SolutionPath"];
        if (!string.IsNullOrWhiteSpace(configured) && (File.Exists(configured) || Directory.Exists(configured)))
            return configured;

        // 2) Walk up from the executing assembly to find IssuePit.slnx
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "src", "IssuePit.slnx");
            if (File.Exists(candidate))
                return candidate;
            // Also check current level for IssuePit.slnx directly (when running from src/)
            candidate = Path.Combine(dir, "IssuePit.slnx");
            if (File.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    private static async Task<(int ExitCode, string Output)> RunDotNetAsync(
        string arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo("dotnet", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start dotnet process.");

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) errorBuilder.AppendLine(e.Data); };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            outputBuilder.AppendLine("\n[Test run timed out or was cancelled]");
        }

        var combined = outputBuilder.ToString();
        if (errorBuilder.Length > 0)
            combined += "\n--- stderr ---\n" + errorBuilder;

        return (process.ExitCode, combined);
    }
}

/// <summary>Represents the result of a test discovery operation.</summary>
public record TestDiscoveryResult(IReadOnlyList<string> Tests, string? Error);

/// <summary>Tracks the state of a single test run.</summary>
public sealed class TestRunInfo
{
    public Guid Id { get; init; }
    public TestRunStatus Status { get; set; }
    public string? Filter { get; init; }
    public string? ProjectPath { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? FinishedAt { get; set; }
    public int? ExitCode { get; set; }
    public string? Output { get; set; }
    public CiCdTestSuite? Suite { get; set; }
}

/// <summary>Status of a test run.</summary>
public enum TestRunStatus
{
    Pending,
    Running,
    Completed,
    Failed,
}
