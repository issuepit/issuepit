using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

/// <summary>
/// Provides test history, flakiness analysis, and comparison endpoints for a project.
/// All endpoints are scoped to <c>/api/projects/{projectId}/test-history</c>.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/test-history")]
public class TestHistoryController(
    IssuePitDbContext db,
    TenantContext ctx) : ControllerBase
{
    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<bool> ProjectExistsInTenantAsync(Guid projectId) =>
        await db.Projects
            .Include(p => p.Organization)
            .AnyAsync(p => p.Id == projectId && p.Organization.TenantId == ctx.CurrentTenant!.Id);

    // ──────────────────────────────────────────────────────────────────────────
    // Test Suite History
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the test suite history for a project: one entry per CI/CD run that produced test results.
    /// Supports filtering by branch and date range.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetHistory(
        Guid projectId,
        [FromQuery] string? branch = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int take = 50)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var query = db.CiCdTestSuites
            .Include(s => s.CiCdRun)
            .Where(s => s.CiCdRun.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(branch))
            query = query.Where(s => s.CiCdRun.Branch == branch);

        if (from.HasValue)
            query = query.Where(s => s.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(s => s.CreatedAt <= to.Value);

        var suites = await query
            .OrderByDescending(s => s.CreatedAt)
            .Take(Math.Min(take, 500))
            .Select(s => new
            {
                s.Id,
                s.ArtifactName,
                s.TotalTests,
                s.PassedTests,
                s.FailedTests,
                s.SkippedTests,
                s.DurationMs,
                s.CreatedAt,
                Run = new
                {
                    s.CiCdRun.Id,
                    s.CiCdRun.CommitSha,
                    s.CiCdRun.Branch,
                    s.CiCdRun.Workflow,
                    s.CiCdRun.StartedAt,
                    s.CiCdRun.ExternalRunId,
                    s.CiCdRun.ExternalSource,
                },
            })
            .ToListAsync();

        return Ok(suites);
    }

    /// <summary>
    /// Returns a list of distinct branches that have test results for this project.
    /// </summary>
    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches(Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var branches = await db.CiCdTestSuites
            .Include(s => s.CiCdRun)
            .Where(s => s.CiCdRun.ProjectId == projectId && s.CiCdRun.Branch != null)
            .Select(s => s.CiCdRun.Branch!)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync();

        return Ok(branches);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Single Test History
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the history of a single test identified by its full name.
    /// Each entry is one occurrence of the test across CI/CD runs.
    /// </summary>
    [HttpGet("tests")]
    public async Task<IActionResult> GetTestHistory(
        Guid projectId,
        [FromQuery] string fullName = "",
        [FromQuery] string? branch = null,
        [FromQuery] int take = 100)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();
        if (string.IsNullOrWhiteSpace(fullName)) return BadRequest("fullName is required.");

        var query = db.CiCdTestCases
            .Include(tc => tc.CiCdTestSuite)
                .ThenInclude(s => s.CiCdRun)
            .Where(tc => tc.CiCdTestSuite.CiCdRun.ProjectId == projectId
                      && tc.FullName == fullName);

        if (!string.IsNullOrWhiteSpace(branch))
            query = query.Where(tc => tc.CiCdTestSuite.CiCdRun.Branch == branch);

        var cases = await query
            .OrderByDescending(tc => tc.CiCdTestSuite.CreatedAt)
            .Take(Math.Min(take, 500))
            .Select(tc => new
            {
                tc.Id,
                tc.FullName,
                tc.ClassName,
                tc.MethodName,
                tc.Outcome,
                OutcomeName = tc.Outcome.ToString(),
                tc.DurationMs,
                tc.ErrorMessage,
                tc.StackTrace,
                Suite = new
                {
                    tc.CiCdTestSuite.Id,
                    tc.CiCdTestSuite.ArtifactName,
                    tc.CiCdTestSuite.CreatedAt,
                    Run = new
                    {
                        tc.CiCdTestSuite.CiCdRun.Id,
                        tc.CiCdTestSuite.CiCdRun.CommitSha,
                        tc.CiCdTestSuite.CiCdRun.Branch,
                        tc.CiCdTestSuite.CiCdRun.StartedAt,
                    },
                },
            })
            .ToListAsync();

        return Ok(cases);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Flakiness Analysis
    // ──────────────────────────────────────────────────────────────────────────

    // A test is flaky when its failure rate falls between these bounds (inclusive).
    private const double FlakyMinFailureRate = 0.05;
    private const double FlakyMaxFailureRate = 0.95;

    /// <summary>
    /// Returns tests that have been both passing and failing across multiple runs (flaky tests).
    /// A test is considered flaky if it has at least <paramref name="minRuns"/> results and
    /// its failure rate is between 5% and 95%.
    /// </summary>
    [HttpGet("flaky")]
    public async Task<IActionResult> GetFlakyTests(
        Guid projectId,
        [FromQuery] string? branch = null,
        [FromQuery] int minRuns = 3,
        [FromQuery] int take = 50)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var query = db.CiCdTestCases
            .Include(tc => tc.CiCdTestSuite)
                .ThenInclude(s => s.CiCdRun)
            .Where(tc => tc.CiCdTestSuite.CiCdRun.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(branch))
            query = query.Where(tc => tc.CiCdTestSuite.CiCdRun.Branch == branch);

        // Load test results grouped by full name.
        var grouped = await query
            .Select(tc => new
            {
                tc.FullName,
                tc.ClassName,
                tc.MethodName,
                tc.Outcome,
                tc.DurationMs,
                tc.CiCdTestSuite.CreatedAt,
            })
            .ToListAsync();

        var flaky = grouped
            .GroupBy(tc => tc.FullName)
            .Where(g => g.Count() >= minRuns)
            .Select(g =>
            {
                var total = g.Count();
                var failed = g.Count(tc => tc.Outcome == TestOutcome.Failed);
                var passed = g.Count(tc => tc.Outcome == TestOutcome.Passed);
                var failRate = (double)failed / total;
                return new
                {
                    FullName = g.Key,
                    ClassName = g.First().ClassName,
                    MethodName = g.First().MethodName,
                    TotalRuns = total,
                    PassedRuns = passed,
                    FailedRuns = failed,
                    FailureRate = failRate,
                    AvgDurationMs = g.Average(tc => tc.DurationMs),
                    LastSeenAt = g.Max(tc => tc.CreatedAt),
                };
            })
            .Where(t => t.FailureRate is >= FlakyMinFailureRate and <= FlakyMaxFailureRate)
            .OrderByDescending(t => t.FailureRate)
            .Take(Math.Min(take, 200))
            .ToList();

        return Ok(flaky);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Compare Two Commits
    // ──────────────────────────────────────────────────────────────────────────

    // A test is considered "significantly slower" when it takes at least this many times longer
    // and the absolute increase exceeds this threshold (milliseconds).
    private const double SlowerTestDurationMultiplier = 1.5;
    private const double SlowerTestMinDeltaMs = 100;

    /// <summary>
    /// Compares test results between two commit SHAs.
    /// Returns new tests, removed tests, newly failing tests, and newly passing tests.
    /// </summary>
    [HttpGet("compare")]
    public async Task<IActionResult> Compare(
        Guid projectId,
        [FromQuery] string baseCommit = "",
        [FromQuery] string headCommit = "")
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();
        if (string.IsNullOrWhiteSpace(baseCommit) || string.IsNullOrWhiteSpace(headCommit))
            return BadRequest("Both baseCommit and headCommit are required.");

        // Get the most recent test suite run for each commit.
        async Task<Dictionary<string, (TestOutcome outcome, double durationMs)>> GetTestsByCommitAsync(string sha)
        {
            var run = await db.CiCdRuns
                .Where(r => r.ProjectId == projectId && r.CommitSha.StartsWith(sha))
                .OrderByDescending(r => r.StartedAt)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (run == Guid.Empty)
                return [];

            var cases = await db.CiCdTestCases
                .Include(tc => tc.CiCdTestSuite)
                .Where(tc => tc.CiCdTestSuite.CiCdRunId == run)
                .Select(tc => new { tc.FullName, tc.Outcome, tc.DurationMs })
                .ToListAsync();

            return cases.GroupBy(tc => tc.FullName)
                .ToDictionary(
                    g => g.Key,
                    g => (g.First().Outcome, g.Average(tc => tc.DurationMs)));
        }

        var baseTests = await GetTestsByCommitAsync(baseCommit);
        var headTests = await GetTestsByCommitAsync(headCommit);

        var newTests = headTests.Keys.Except(baseTests.Keys)
            .Select(k => new { FullName = k, headTests[k].outcome, OutcomeName = headTests[k].outcome.ToString(), headTests[k].durationMs })
            .OrderBy(t => t.FullName)
            .ToList();

        var removedTests = baseTests.Keys.Except(headTests.Keys)
            .Select(k => new { FullName = k, baseTests[k].outcome, OutcomeName = baseTests[k].outcome.ToString(), baseTests[k].durationMs })
            .OrderBy(t => t.FullName)
            .ToList();

        var commonTests = headTests.Keys.Intersect(baseTests.Keys).ToList();

        var nowFailing = commonTests
            .Where(k => baseTests[k].outcome == TestOutcome.Passed && headTests[k].outcome == TestOutcome.Failed)
            .Select(k => new { FullName = k, BaseOutcomeName = baseTests[k].outcome.ToString(), HeadOutcomeName = headTests[k].outcome.ToString(), headTests[k].durationMs })
            .OrderBy(t => t.FullName)
            .ToList();

        var nowPassing = commonTests
            .Where(k => baseTests[k].outcome == TestOutcome.Failed && headTests[k].outcome == TestOutcome.Passed)
            .Select(k => new { FullName = k, BaseOutcomeName = baseTests[k].outcome.ToString(), HeadOutcomeName = headTests[k].outcome.ToString(), headTests[k].durationMs })
            .OrderBy(t => t.FullName)
            .ToList();

        var slowerTests = commonTests
            .Where(k => headTests[k].durationMs > baseTests[k].durationMs * SlowerTestDurationMultiplier && headTests[k].durationMs - baseTests[k].durationMs > SlowerTestMinDeltaMs)
            .Select(k => new { FullName = k, BaseDurationMs = baseTests[k].durationMs, HeadDurationMs = headTests[k].durationMs, DeltaMs = headTests[k].durationMs - baseTests[k].durationMs })
            .OrderByDescending(t => t.DeltaMs)
            .ToList();

        return Ok(new
        {
            BaseCommit = baseCommit,
            HeadCommit = headCommit,
            BaseTestCount = baseTests.Count,
            HeadTestCount = headTests.Count,
            NewTests = newTests,
            RemovedTests = removedTests,
            NowFailing = nowFailing,
            NowPassing = nowPassing,
            SlowerTests = slowerTests,
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Search
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Full-text search across test names, error messages, and stack traces.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        Guid projectId,
        [FromQuery] string q = "",
        [FromQuery] string? branch = null,
        [FromQuery] TestOutcome? outcome = null,
        [FromQuery] int take = 50)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();
        if (string.IsNullOrWhiteSpace(q)) return BadRequest("q is required.");

        var query = db.CiCdTestCases
            .Include(tc => tc.CiCdTestSuite)
                .ThenInclude(s => s.CiCdRun)
            .Where(tc => tc.CiCdTestSuite.CiCdRun.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(branch))
            query = query.Where(tc => tc.CiCdTestSuite.CiCdRun.Branch == branch);

        if (outcome.HasValue)
            query = query.Where(tc => tc.Outcome == outcome.Value);

        // Case-insensitive search across name, error message, and stack trace.
        var lower = q.ToLower();
        query = query.Where(tc =>
            tc.FullName.ToLower().Contains(lower) ||
            (tc.ErrorMessage != null && tc.ErrorMessage.ToLower().Contains(lower)) ||
            (tc.StackTrace != null && tc.StackTrace.ToLower().Contains(lower)));

        var results = await query
            .OrderByDescending(tc => tc.CiCdTestSuite.CreatedAt)
            .Take(Math.Min(take, 200))
            .Select(tc => new
            {
                tc.Id,
                tc.FullName,
                tc.ClassName,
                tc.MethodName,
                tc.Outcome,
                OutcomeName = tc.Outcome.ToString(),
                tc.DurationMs,
                tc.ErrorMessage,
                tc.StackTrace,
                Suite = new
                {
                    tc.CiCdTestSuite.Id,
                    tc.CiCdTestSuite.ArtifactName,
                    tc.CiCdTestSuite.CreatedAt,
                    Run = new
                    {
                        tc.CiCdTestSuite.CiCdRun.Id,
                        tc.CiCdTestSuite.CiCdRun.CommitSha,
                        tc.CiCdTestSuite.CiCdRun.Branch,
                        tc.CiCdTestSuite.CiCdRun.StartedAt,
                    },
                },
            })
            .ToListAsync();

        return Ok(results);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Import TRX
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Imports a <c>.trx</c> file directly, creating a CI/CD run and saving the parsed test results.
    /// Useful for manually uploading test results from local or external builds.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
    public async Task<IActionResult> ImportTrx(
        Guid projectId,
        IFormFile file,
        [FromForm] string? commitSha = null,
        [FromForm] string? branch = null,
        [FromForm] string? workflow = null,
        [FromForm] string? artifactName = null)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        if (file is null || file.Length == 0)
            return BadRequest("A .trx file is required.");

        var fileName = Path.GetFileName(file.FileName);
        if (!fileName.EndsWith(".trx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .trx files are supported.");

        // Parse the TRX stream.
        using var stream = file.OpenReadStream();
        var name = artifactName ?? Path.GetFileNameWithoutExtension(fileName);
        var suite = TrxParser.Parse(stream, name);

        if (suite is null)
            return BadRequest("Failed to parse the TRX file. Ensure it is a valid Visual Studio test results file.");

        // Create a synthetic CI/CD run for this import.
        var run = new CiCdRun
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CommitSha = !string.IsNullOrWhiteSpace(commitSha) ? commitSha.Trim() : "imported",
            Branch = string.IsNullOrWhiteSpace(branch) ? null : branch.Trim(),
            Workflow = string.IsNullOrWhiteSpace(workflow) ? "manual-import" : workflow.Trim(),
            Status = CiCdRunStatus.Succeeded,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            EventName = "manual-import",
        };

        suite.Id = Guid.NewGuid();
        suite.CiCdRunId = run.Id;
        suite.CreatedAt = DateTime.UtcNow;
        foreach (var tc in suite.TestCases)
            tc.Id = Guid.NewGuid();

        db.CiCdRuns.Add(run);
        db.CiCdTestSuites.Add(suite);
        await db.SaveChangesAsync();

        return Ok(new
        {
            RunId = run.Id,
            suite.Id,
            suite.ArtifactName,
            suite.TotalTests,
            suite.PassedTests,
            suite.FailedTests,
            suite.SkippedTests,
            suite.DurationMs,
        });
    }
}
