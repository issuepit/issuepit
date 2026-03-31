using IssuePit.Core.Enums;
using IssuePit.TestRunner.Services;
using Microsoft.AspNetCore.Mvc;

namespace IssuePit.TestRunner.Controllers;

/// <summary>
/// Exposes endpoints for discovering, executing, and retrieving .NET test results.
/// Works against the currently running Aspire environment.
/// </summary>
[ApiController]
[Route("api/tests")]
public class TestRunnerController : ControllerBase
{
    private readonly DotNetTestRunnerService _runner;

    public TestRunnerController(DotNetTestRunnerService runner) => _runner = runner;

    /// <summary>
    /// Discovers available test cases from the configured solution or project path.
    /// </summary>
    [HttpGet("discover")]
    public async Task<IActionResult> DiscoverTests(CancellationToken cancellationToken)
    {
        var result = await _runner.DiscoverTestsAsync(cancellationToken);
        if (result.Error is not null)
            return Ok(new TestDiscoveryResponse(result.Tests, result.Error));
        return Ok(new TestDiscoveryResponse(result.Tests, null));
    }

    /// <summary>
    /// Triggers a new test run. Tests execute asynchronously; poll
    /// <c>GET /api/tests/runs/{runId}</c> for results.
    /// </summary>
    [HttpPost("run")]
    public IActionResult StartRun([FromBody] StartTestRunRequest? request)
    {
        var run = _runner.StartRun(request?.Filter, request?.Project);
        return Accepted(new TestRunResponse(run));
    }

    /// <summary>Returns a list of all recent test runs.</summary>
    [HttpGet("runs")]
    public IActionResult ListRuns()
    {
        var runs = _runner.ListRuns();
        return Ok(runs.Select(r => new TestRunResponse(r)).ToList());
    }

    /// <summary>Returns the details and results of a specific test run.</summary>
    [HttpGet("runs/{runId:guid}")]
    public IActionResult GetRun(Guid runId)
    {
        var run = _runner.GetRun(runId);
        if (run is null)
            return NotFound();
        return Ok(new TestRunDetailResponse(run));
    }

    /// <summary>Returns the raw console output of a specific test run.</summary>
    [HttpGet("runs/{runId:guid}/output")]
    public IActionResult GetRunOutput(Guid runId)
    {
        var run = _runner.GetRun(runId);
        if (run is null)
            return NotFound();
        return Ok(new TestRunOutputResponse(run.Id, run.Output));
    }
}

// --- Response / Request records ---

public record TestDiscoveryResponse(IReadOnlyList<string> Tests, string? Error);

public record StartTestRunRequest(string? Filter, string? Project);

public record TestRunResponse(
    Guid Id,
    string Status,
    string? Filter,
    DateTime StartedAt,
    DateTime? FinishedAt,
    int TotalTests,
    int PassedTests,
    int FailedTests,
    int SkippedTests)
{
    public TestRunResponse(TestRunInfo run)
        : this(
            run.Id,
            run.Status.ToString(),
            run.Filter,
            run.StartedAt,
            run.FinishedAt,
            run.Suite?.TotalTests ?? 0,
            run.Suite?.PassedTests ?? 0,
            run.Suite?.FailedTests ?? 0,
            run.Suite?.SkippedTests ?? 0)
    { }
}

public record TestRunDetailResponse(
    Guid Id,
    string Status,
    string? Filter,
    string? ProjectPath,
    DateTime StartedAt,
    DateTime? FinishedAt,
    int? ExitCode,
    int TotalTests,
    int PassedTests,
    int FailedTests,
    int SkippedTests,
    double DurationMs,
    IReadOnlyList<TestCaseResponse>? TestCases)
{
    public TestRunDetailResponse(TestRunInfo run)
        : this(
            run.Id,
            run.Status.ToString(),
            run.Filter,
            run.ProjectPath,
            run.StartedAt,
            run.FinishedAt,
            run.ExitCode,
            run.Suite?.TotalTests ?? 0,
            run.Suite?.PassedTests ?? 0,
            run.Suite?.FailedTests ?? 0,
            run.Suite?.SkippedTests ?? 0,
            run.Suite?.DurationMs ?? 0,
            run.Suite?.TestCases?.Select(tc => new TestCaseResponse(
                tc.FullName,
                tc.ClassName,
                tc.MethodName,
                tc.Outcome.ToString(),
                tc.DurationMs,
                tc.ErrorMessage,
                tc.StackTrace)).ToList())
    { }
}

public record TestCaseResponse(
    string FullName,
    string? ClassName,
    string? MethodName,
    string Outcome,
    double DurationMs,
    string? ErrorMessage,
    string? StackTrace);

public record TestRunOutputResponse(Guid RunId, string? Output);
