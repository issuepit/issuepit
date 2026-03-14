using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

/// <summary>
/// Provides aggregated test history across CI/CD runs for a project.
/// All endpoints are scoped to <c>/api/projects/{projectId}/test-history</c>.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/test-history")]
public class TestHistoryController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    // ──────────────────────────────────────────────────────────────────────────
    // Run summaries
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns per-run test summary rows for the given project, newest first.
    /// Each row aggregates all test suites in that run.
    /// Optionally filtered by branch.
    /// </summary>
    [HttpGet("runs")]
    public async Task<IActionResult> GetRunSummaries(
        Guid projectId,
        [FromQuery] string? branch = null,
        [FromQuery] int take = 50)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var query = db.CiCdTestSuites
            .Where(s => s.CiCdRun.ProjectId == projectId
                     && s.CiCdRun.Project.Organization.TenantId == ctx.CurrentTenant.Id);

        if (!string.IsNullOrWhiteSpace(branch))
            query = query.Where(s => s.CiCdRun.Branch == branch);

        // Group suites by run and aggregate counts.
        var rows = await query
            .GroupBy(s => new
            {
                s.CiCdRunId,
                s.CiCdRun.CommitSha,
                s.CiCdRun.Branch,
                s.CiCdRun.StartedAt,
                s.CiCdRun.EndedAt,
                s.CiCdRun.Status,
            })
            .Select(g => new
            {
                RunId = g.Key.CiCdRunId,
                g.Key.CommitSha,
                g.Key.Branch,
                g.Key.StartedAt,
                g.Key.EndedAt,
                StatusName = g.Key.Status.ToString(),
                TotalTests = g.Sum(s => s.TotalTests),
                PassedTests = g.Sum(s => s.PassedTests),
                FailedTests = g.Sum(s => s.FailedTests),
                SkippedTests = g.Sum(s => s.SkippedTests),
                DurationMs = g.Sum(s => s.DurationMs),
                SuiteCount = g.Count(),
            })
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Min(take, 200))
            .ToListAsync();

        return Ok(rows);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test list with flakiness stats
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns aggregated stats for every unique test in the project, optionally
    /// filtered by branch and/or a search string (matched against the full test name).
    /// Tests are sorted by failure count descending (most flaky first).
    /// </summary>
    [HttpGet("tests")]
    public async Task<IActionResult> GetTests(
        Guid projectId,
        [FromQuery] string? branch = null,
        [FromQuery] string? search = null,
        [FromQuery] int take = 200)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var query = db.CiCdTestCases
            .Where(tc => tc.CiCdTestSuite.CiCdRun.ProjectId == projectId
                      && tc.CiCdTestSuite.CiCdRun.Project.Organization.TenantId == ctx.CurrentTenant.Id);

        if (!string.IsNullOrWhiteSpace(branch))
            query = query.Where(tc => tc.CiCdTestSuite.CiCdRun.Branch == branch);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(tc => tc.FullName.Contains(search));

        var tests = await query
            .GroupBy(tc => new { tc.FullName, tc.ClassName, tc.MethodName })
            .Select(g => new
            {
                g.Key.FullName,
                g.Key.ClassName,
                g.Key.MethodName,
                TotalRuns = g.Count(),
                PassedRuns = g.Count(tc => tc.Outcome == TestOutcome.Passed),
                FailedRuns = g.Count(tc => tc.Outcome == TestOutcome.Failed),
                SkippedRuns = g.Count(tc => tc.Outcome == TestOutcome.Skipped || tc.Outcome == TestOutcome.NotExecuted),
                AvgDurationMs = g.Average(tc => tc.DurationMs),
                LastOutcome = g.OrderByDescending(tc => tc.CiCdTestSuite.CreatedAt).Select(tc => tc.Outcome).FirstOrDefault(),
                LastOutcomeName = g.OrderByDescending(tc => tc.CiCdTestSuite.CreatedAt).Select(tc => tc.Outcome.ToString()).FirstOrDefault(),
                LastRunAt = g.Max(tc => tc.CiCdTestSuite.CreatedAt),
            })
            .OrderByDescending(t => t.FailedRuns)
            .ThenBy(t => t.FullName)
            .Take(Math.Min(take, 2000))
            .ToListAsync();

        return Ok(tests);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Per-test history
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the run-by-run history for a single test identified by its full name.
    /// Optionally filtered by branch.
    /// </summary>
    [HttpGet("tests/{fullName}")]
    public async Task<IActionResult> GetTestHistory(
        Guid projectId,
        string fullName,
        [FromQuery] string? branch = null,
        [FromQuery] int take = 50)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var decodedName = Uri.UnescapeDataString(fullName);

        var query = db.CiCdTestCases
            .Where(tc => tc.FullName == decodedName
                      && tc.CiCdTestSuite.CiCdRun.ProjectId == projectId
                      && tc.CiCdTestSuite.CiCdRun.Project.Organization.TenantId == ctx.CurrentTenant.Id);

        if (!string.IsNullOrWhiteSpace(branch))
            query = query.Where(tc => tc.CiCdTestSuite.CiCdRun.Branch == branch);

        var history = await query
            .OrderByDescending(tc => tc.CiCdTestSuite.CreatedAt)
            .Take(Math.Min(take, 200))
            .Select(tc => new
            {
                tc.Id,
                tc.Outcome,
                OutcomeName = tc.Outcome.ToString(),
                tc.DurationMs,
                tc.ErrorMessage,
                tc.StackTrace,
                RunId = tc.CiCdTestSuite.CiCdRunId,
                tc.CiCdTestSuite.CiCdRun.CommitSha,
                tc.CiCdTestSuite.CiCdRun.Branch,
                RunAt = tc.CiCdTestSuite.CreatedAt,
                ArtifactName = tc.CiCdTestSuite.ArtifactName,
            })
            .ToListAsync();

        return Ok(history);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Import TRX file directly
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Imports a <c>.trx</c> file directly into the test history for the given project.
    /// A synthetic <see cref="CiCdRun"/> row is created with the supplied metadata so the
    /// imported results integrate seamlessly with the rest of the test history.
    /// </summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportTrx(
        Guid projectId,
        IFormFile file,
        [FromForm] string? commitSha = null,
        [FromForm] string? branch = null,
        [FromForm] string? artifactName = null)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        if (file is null || file.Length == 0)
            return BadRequest("A .trx file is required.");

        if (!file.FileName.EndsWith(".trx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .trx files are supported.");

        CiCdTestSuite? suite;
        await using (var stream = file.OpenReadStream())
        {
            var name = artifactName ?? Path.GetFileNameWithoutExtension(file.FileName);
            suite = TrxParser.Parse(stream, name);
        }

        if (suite is null)
            return BadRequest("Failed to parse the uploaded .trx file. Make sure it is a valid Visual Studio test results file.");

        // Create a synthetic run to anchor the imported results.
        var now = DateTime.UtcNow;
        var syntheticRun = new CiCdRun
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CommitSha = commitSha?.Trim() ?? string.Empty,
            Branch = string.IsNullOrWhiteSpace(branch) ? null : branch,
            Status = CiCdRunStatus.Succeeded,
            StartedAt = now,
            EndedAt = now,
            ExternalSource = "import",
        };

        suite.CiCdRunId = syntheticRun.Id;

        db.CiCdRuns.Add(syntheticRun);
        db.CiCdTestSuites.Add(suite);
        await db.SaveChangesAsync();

        return Ok(new
        {
            runId = syntheticRun.Id,
            suiteId = suite.Id,
            suite.TotalTests,
            suite.PassedTests,
            suite.FailedTests,
            suite.SkippedTests,
            suite.DurationMs,
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<bool> ProjectExistsInTenantAsync(Guid projectId) =>
        await db.Projects.AnyAsync(p => p.Id == projectId
                                     && p.Organization.TenantId == ctx.CurrentTenant!.Id);
}
