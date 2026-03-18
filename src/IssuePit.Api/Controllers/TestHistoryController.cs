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
    // Daily summary (chart data)
    // ──────────────────────────────────────────────────────────────────────────

    // ──────────────────────────────────────────────────────────────────────────
    // Branches
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the distinct branch names that have test runs in this project.
    /// </summary>
    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches(Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var branches = await db.CiCdTestSuites
            .Where(s => s.CiCdRun.ProjectId == projectId
                     && s.CiCdRun.Project.Organization.TenantId == ctx.CurrentTenant.Id
                     && s.CiCdRun.Branch != null)
            .Select(s => s.CiCdRun.Branch!)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync();

        return Ok(branches);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Daily summary (chart data)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns per-day aggregated test counts for the given project, ordered by date ascending.
    /// Each day includes a <c>groups</c> array with per-artifact-name breakdowns, enabling
    /// stacked-bar charts per test group (unit, e2e, integration, …).
    /// Optionally filtered by one or more branches and limited to the most recent <paramref name="days"/> calendar days.
    /// </summary>
    [HttpGet("daily-summary")]
    public async Task<IActionResult> GetDailySummary(
        Guid projectId,
        [FromQuery] string[]? branch = null,
        [FromQuery] int days = 30)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        const int MaxDailySummaryDays = 90;
        days = Math.Clamp(days, 1, MaxDailySummaryDays);
        var since = DateTime.UtcNow.Date.AddDays(-days + 1);

        var query = db.CiCdTestSuites
            .Where(s => s.CiCdRun.ProjectId == projectId
                     && s.CiCdRun.Project.Organization.TenantId == ctx.CurrentTenant.Id
                     && s.CiCdRun.StartedAt >= since);

        if (branch is { Length: > 0 })
            query = query.Where(s => branch.Contains(s.CiCdRun.Branch));

        var raw = await query
            .Select(s => new
            {
                Date = s.CiCdRun.StartedAt.Date,
                s.ArtifactName,
                s.TotalTests,
                s.PassedTests,
                s.FailedTests,
                s.SkippedTests,
                s.DurationMs,
            })
            .ToListAsync();

        var result = raw
            .GroupBy(s => s.Date)
            .Select(g => new
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                TotalTests = g.Sum(s => s.TotalTests),
                PassedTests = g.Sum(s => s.PassedTests),
                FailedTests = g.Sum(s => s.FailedTests),
                SkippedTests = g.Sum(s => s.SkippedTests),
                DurationMs = g.Sum(s => s.DurationMs),
                RunCount = g.Count(),
                Groups = g
                    .GroupBy(s => s.ArtifactName)
                    .Select(ag => new
                    {
                        Name = ag.Key,
                        TotalTests = ag.Sum(s => s.TotalTests),
                        PassedTests = ag.Sum(s => s.PassedTests),
                        FailedTests = ag.Sum(s => s.FailedTests),
                        SkippedTests = ag.Sum(s => s.SkippedTests),
                        DurationMs = ag.Sum(s => s.DurationMs),
                    })
                    .OrderBy(ag => ag.Name)
                    .ToList(),
            })
            .OrderBy(r => r.Date)
            .ToList();

        return Ok(result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Run summaries
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns per-run test summary rows for the given project, newest first.
    /// Each row aggregates all test suites in that run.
    /// Optionally filtered by one or more branches.
    /// </summary>
    [HttpGet("runs")]
    public async Task<IActionResult> GetRunSummaries(
        Guid projectId,
        [FromQuery] string[]? branch = null,
        [FromQuery] int take = 50)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var query = db.CiCdTestSuites
            .Where(s => s.CiCdRun.ProjectId == projectId
                     && s.CiCdRun.Project.Organization.TenantId == ctx.CurrentTenant.Id);

        if (branch is { Length: > 0 })
            query = query.Where(s => branch.Contains(s.CiCdRun.Branch));

        // Materialise raw suite rows first so that nested client-side grouping (Groups) works.
        var raw = await query
            .Select(s => new
            {
                s.CiCdRunId,
                s.CiCdRun.CommitSha,
                s.CiCdRun.Branch,
                s.CiCdRun.StartedAt,
                s.CiCdRun.EndedAt,
                StatusName = s.CiCdRun.Status.ToString(),
                s.ArtifactName,
                s.TotalTests,
                s.PassedTests,
                s.FailedTests,
                s.SkippedTests,
                s.DurationMs,
            })
            .ToListAsync();

        var rows = raw
            .GroupBy(s => s.CiCdRunId)
            .Select(g => new
            {
                RunId = g.Key,
                CommitSha = g.First().CommitSha,
                Branch = g.First().Branch,
                StartedAt = g.First().StartedAt,
                EndedAt = g.First().EndedAt,
                StatusName = g.First().StatusName,
                TotalTests = g.Sum(s => s.TotalTests),
                PassedTests = g.Sum(s => s.PassedTests),
                FailedTests = g.Sum(s => s.FailedTests),
                SkippedTests = g.Sum(s => s.SkippedTests),
                DurationMs = g.Sum(s => s.DurationMs),
                SuiteCount = g.Count(),
                Groups = g
                    .GroupBy(s => s.ArtifactName)
                    .Select(ag => new
                    {
                        Name = ag.Key,
                        TotalTests = ag.Sum(s => s.TotalTests),
                        PassedTests = ag.Sum(s => s.PassedTests),
                        FailedTests = ag.Sum(s => s.FailedTests),
                        SkippedTests = ag.Sum(s => s.SkippedTests),
                        DurationMs = ag.Sum(s => s.DurationMs),
                    })
                    .OrderBy(ag => ag.Name)
                    .ToList(),
            })
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Min(take, 200))
            .ToList();

        return Ok(rows);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test list with flakiness stats
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns aggregated stats for every unique test in the project, optionally
    /// filtered by one or more branches and/or a search string (matched against the full test name).
    /// Tests are sorted by failure count descending (most flaky first).
    /// </summary>
    [HttpGet("tests")]
    public async Task<IActionResult> GetTests(
        Guid projectId,
        [FromQuery] string[]? branch = null,
        [FromQuery] string? search = null,
        [FromQuery] int take = 200)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var query = db.CiCdTestCases
            .Where(tc => tc.CiCdTestSuite.CiCdRun.ProjectId == projectId
                      && tc.CiCdTestSuite.CiCdRun.Project.Organization.TenantId == ctx.CurrentTenant.Id);

        if (branch is { Length: > 0 })
            query = query.Where(tc => branch.Contains(tc.CiCdTestSuite.CiCdRun.Branch));

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
    /// Optionally filtered by one or more branches.
    /// </summary>
    [HttpGet("tests/{fullName}")]
    public async Task<IActionResult> GetTestHistory(
        Guid projectId,
        string fullName,
        [FromQuery] string[]? branch = null,
        [FromQuery] int take = 50)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var decodedName = Uri.UnescapeDataString(fullName);

        var query = db.CiCdTestCases
            .Where(tc => tc.FullName == decodedName
                      && tc.CiCdTestSuite.CiCdRun.ProjectId == projectId
                      && tc.CiCdTestSuite.CiCdRun.Project.Organization.TenantId == ctx.CurrentTenant.Id);

        if (branch is { Length: > 0 })
            query = query.Where(tc => branch.Contains(tc.CiCdTestSuite.CiCdRun.Branch));

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
    // Compare two runs
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Compares the test cases of two CI/CD runs and returns categorised diffs:
    /// new tests (only in run B), removed tests (only in run A),
    /// fixed tests (failed in A, passed in B), regressed tests (passed in A, failed in B),
    /// and unchanged tests still failing in both.
    /// </summary>
    [HttpGet("compare")]
    public async Task<IActionResult> CompareRuns(
        Guid projectId,
        [FromQuery] Guid runA,
        [FromQuery] Guid runB)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        // Verify both runs belong to this project+tenant.
        var runIds = new[] { runA, runB };
        var validRuns = await db.CiCdRuns
            .Where(r => runIds.Contains(r.Id)
                     && r.ProjectId == projectId
                     && r.Project.Organization.TenantId == ctx.CurrentTenant.Id)
            .Select(r => new { r.Id, r.CommitSha, r.Branch, r.StartedAt })
            .ToListAsync();

        if (validRuns.Count != 2)
            return BadRequest("Both runA and runB must belong to this project.");

        var runAMeta = validRuns.First(r => r.Id == runA);
        var runBMeta = validRuns.First(r => r.Id == runB);

        // Load all test cases for both runs.
        var testsA = await db.CiCdTestCases
            .Where(tc => tc.CiCdTestSuite.CiCdRunId == runA)
            .Select(tc => new { tc.FullName, tc.Outcome, tc.DurationMs, tc.ErrorMessage })
            .ToListAsync();

        var testsB = await db.CiCdTestCases
            .Where(tc => tc.CiCdTestSuite.CiCdRunId == runB)
            .Select(tc => new { tc.FullName, tc.Outcome, tc.DurationMs, tc.ErrorMessage })
            .ToListAsync();

        var namesA = testsA.ToDictionary(t => t.FullName, t => t);
        var namesB = testsB.ToDictionary(t => t.FullName, t => t);

        var added = namesB.Keys.Except(namesA.Keys)
            .Select(n => new { fullName = n, outcomeName = namesB[n].Outcome.ToString(), durationMs = namesB[n].DurationMs })
            .OrderBy(t => t.fullName)
            .ToList();

        var removed = namesA.Keys.Except(namesB.Keys)
            .Select(n => new { fullName = n, outcomeName = namesA[n].Outcome.ToString(), durationMs = namesA[n].DurationMs })
            .OrderBy(t => t.fullName)
            .ToList();

        var both = namesA.Keys.Intersect(namesB.Keys).ToList();

        var fixed_ = both
            .Where(n => namesA[n].Outcome == TestOutcome.Failed && namesB[n].Outcome == TestOutcome.Passed)
            .Select(n => new { fullName = n, durationMsA = namesA[n].DurationMs, durationMsB = namesB[n].DurationMs })
            .OrderBy(t => t.fullName)
            .ToList();

        var regressed = both
            .Where(n => namesA[n].Outcome == TestOutcome.Passed && namesB[n].Outcome == TestOutcome.Failed)
            .Select(n => new { fullName = n, durationMsA = namesA[n].DurationMs, durationMsB = namesB[n].DurationMs, errorMessage = namesB[n].ErrorMessage })
            .OrderBy(t => t.fullName)
            .ToList();

        var slowedDown = both
            .Where(n => namesB[n].DurationMs > namesA[n].DurationMs * 1.5 && namesA[n].DurationMs > 50)
            .Select(n => new { fullName = n, durationMsA = namesA[n].DurationMs, durationMsB = namesB[n].DurationMs, deltaMs = namesB[n].DurationMs - namesA[n].DurationMs })
            .OrderByDescending(t => t.deltaMs)
            .Take(20)
            .ToList();

        return Ok(new
        {
            runA = new { runAMeta.Id, runAMeta.CommitSha, runAMeta.Branch, runAMeta.StartedAt, testCount = testsA.Count },
            runB = new { runBMeta.Id, runBMeta.CommitSha, runBMeta.Branch, runBMeta.StartedAt, testCount = testsB.Count },
            added,
            removed,
            fixed_,
            regressed,
            slowedDown,
            summary = new
            {
                addedCount = added.Count,
                removedCount = removed.Count,
                fixedCount = fixed_.Count,
                regressedCount = regressed.Count,
                slowedDownCount = slowedDown.Count,
            },
        });
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

        return Ok(new ImportTrxResponse(
            syntheticRun.Id,
            suite.Id,
            suite.TotalTests,
            suite.PassedTests,
            suite.FailedTests,
            suite.SkippedTests,
            suite.DurationMs));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Import Cobertura coverage directly
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Imports a Cobertura XML coverage file directly into the coverage history for the given project.
    /// A synthetic <see cref="CiCdRun"/> row is created to anchor the imported coverage report.
    /// </summary>
    [HttpPost("coverage/import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportCoverage(
        Guid projectId,
        IFormFile file,
        [FromForm] string? commitSha = null,
        [FromForm] string? branch = null,
        [FromForm] string? artifactName = null)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        if (file is null || file.Length == 0)
            return BadRequest("A Cobertura XML file is required.");

        CiCdCoverageReport? report;
        await using (var stream = file.OpenReadStream())
        {
            var name = artifactName ?? Path.GetFileNameWithoutExtension(file.FileName);
            report = CoberturaParser.Parse(stream, name);
        }

        if (report is null)
            return BadRequest("Failed to parse the uploaded file. Make sure it is a valid Cobertura XML coverage report.");

        // Create a synthetic run to anchor the imported report.
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

        report.CiCdRunId = syntheticRun.Id;

        db.CiCdRuns.Add(syntheticRun);
        db.CiCdCoverageReports.Add(report);
        await db.SaveChangesAsync();

        return Ok(new ImportCoverageResponse(
            syntheticRun.Id,
            report.Id,
            report.LineRate,
            report.BranchRate,
            report.LinesCovered,
            report.LinesValid,
            report.BranchesCovered,
            report.BranchesValid));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Coverage reports
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns per-run coverage summary rows for the given project, newest first.
    /// Each row aggregates all coverage reports in that run.
    /// Optionally filtered by branch.
    /// </summary>
    [HttpGet("coverage/runs")]
    public async Task<IActionResult> GetCoverageRunSummaries(
        Guid projectId,
        [FromQuery] string? branch = null,
        [FromQuery] int take = 50)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var query = db.CiCdCoverageReports
            .Where(r => r.CiCdRun.ProjectId == projectId
                     && r.CiCdRun.Project.Organization.TenantId == ctx.CurrentTenant.Id);

        if (!string.IsNullOrWhiteSpace(branch))
            query = query.Where(r => r.CiCdRun.Branch == branch);

        var raw = await query
            .Select(r => new
            {
                r.CiCdRunId,
                r.CiCdRun.CommitSha,
                r.CiCdRun.Branch,
                r.CiCdRun.StartedAt,
                r.CiCdRun.EndedAt,
                StatusName = r.CiCdRun.Status.ToString(),
                r.ArtifactName,
                r.LineRate,
                r.BranchRate,
                r.LinesCovered,
                r.LinesValid,
                r.BranchesCovered,
                r.BranchesValid,
            })
            .ToListAsync();

        var rows = raw
            .GroupBy(r => r.CiCdRunId)
            .Select(g =>
            {
                // Aggregate line coverage: weighted average by LinesValid.
                var totalLinesValid = g.Sum(r => r.LinesValid);
                var totalLinesCovered = g.Sum(r => r.LinesCovered);
                var totalBranchesValid = g.Sum(r => r.BranchesValid);
                var totalBranchesCovered = g.Sum(r => r.BranchesCovered);

                // Prefer computed rates from absolute counts; fall back to average of rates.
                var lineRate = totalLinesValid > 0
                    ? (double)totalLinesCovered / totalLinesValid
                    : g.Average(r => r.LineRate);
                var branchRate = totalBranchesValid > 0
                    ? (double)totalBranchesCovered / totalBranchesValid
                    : g.Average(r => r.BranchRate);

                return new
                {
                    RunId = g.Key,
                    g.First().CommitSha,
                    g.First().Branch,
                    g.First().StartedAt,
                    g.First().EndedAt,
                    g.First().StatusName,
                    LineRate = lineRate,
                    BranchRate = branchRate,
                    LinesCovered = totalLinesCovered,
                    LinesValid = totalLinesValid,
                    BranchesCovered = totalBranchesCovered,
                    BranchesValid = totalBranchesValid,
                    ReportCount = g.Count(),
                    Reports = g
                        .GroupBy(r => r.ArtifactName)
                        .Select(ag => new
                        {
                            Name = ag.Key,
                            LineRate = ag.Sum(r => r.LinesValid) > 0
                                ? (double)ag.Sum(r => r.LinesCovered) / ag.Sum(r => r.LinesValid)
                                : ag.Average(r => r.LineRate),
                            BranchRate = ag.Sum(r => r.BranchesValid) > 0
                                ? (double)ag.Sum(r => r.BranchesCovered) / ag.Sum(r => r.BranchesValid)
                                : ag.Average(r => r.BranchRate),
                            LinesCovered = ag.Sum(r => r.LinesCovered),
                            LinesValid = ag.Sum(r => r.LinesValid),
                            BranchesCovered = ag.Sum(r => r.BranchesCovered),
                            BranchesValid = ag.Sum(r => r.BranchesValid),
                        })
                        .OrderBy(ag => ag.Name)
                        .ToList(),
                };
            })
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Min(take, 200))
            .ToList();

        return Ok(rows);
    }

    /// <summary>
    /// Returns per-day aggregated coverage data for the given project, ordered by date ascending.
    /// Optionally filtered by branch and limited to the most recent <paramref name="days"/> calendar days.
    /// </summary>
    [HttpGet("coverage/daily-summary")]
    public async Task<IActionResult> GetCoverageDailySummary(
        Guid projectId,
        [FromQuery] string? branch = null,
        [FromQuery] int days = 30)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        const int MaxDailySummaryDays = 90;
        days = Math.Clamp(days, 1, MaxDailySummaryDays);
        var since = DateTime.UtcNow.Date.AddDays(-days + 1);

        var query = db.CiCdCoverageReports
            .Where(r => r.CiCdRun.ProjectId == projectId
                     && r.CiCdRun.Project.Organization.TenantId == ctx.CurrentTenant.Id
                     && r.CiCdRun.StartedAt >= since);

        if (!string.IsNullOrWhiteSpace(branch))
            query = query.Where(r => r.CiCdRun.Branch == branch);

        var raw = await query
            .Select(r => new
            {
                Date = r.CiCdRun.StartedAt.Date,
                r.ArtifactName,
                r.LineRate,
                r.BranchRate,
                r.LinesCovered,
                r.LinesValid,
                r.BranchesCovered,
                r.BranchesValid,
            })
            .ToListAsync();

        var result = raw
            .GroupBy(r => r.Date)
            .Select(g =>
            {
                var totalLinesValid = g.Sum(r => r.LinesValid);
                var totalLinesCovered = g.Sum(r => r.LinesCovered);
                var totalBranchesValid = g.Sum(r => r.BranchesValid);
                var totalBranchesCovered = g.Sum(r => r.BranchesCovered);

                return new
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    LineRate = totalLinesValid > 0
                        ? (double)totalLinesCovered / totalLinesValid
                        : g.Average(r => r.LineRate),
                    BranchRate = totalBranchesValid > 0
                        ? (double)totalBranchesCovered / totalBranchesValid
                        : g.Average(r => r.BranchRate),
                    LinesCovered = totalLinesCovered,
                    LinesValid = totalLinesValid,
                    BranchesCovered = totalBranchesCovered,
                    BranchesValid = totalBranchesValid,
                    RunCount = g.Select(r => r.Date).Distinct().Count(),
                    Reports = g
                        .GroupBy(r => r.ArtifactName)
                        .Select(ag => new
                        {
                            Name = ag.Key,
                            LineRate = ag.Sum(r => r.LinesValid) > 0
                                ? (double)ag.Sum(r => r.LinesCovered) / ag.Sum(r => r.LinesValid)
                                : ag.Average(r => r.LineRate),
                            BranchRate = ag.Sum(r => r.BranchesValid) > 0
                                ? (double)ag.Sum(r => r.BranchesCovered) / ag.Sum(r => r.BranchesValid)
                                : ag.Average(r => r.BranchRate),
                        })
                        .OrderBy(ag => ag.Name)
                        .ToList(),
                };
            })
            .OrderBy(r => r.Date)
            .ToList();

        return Ok(result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<bool> ProjectExistsInTenantAsync(Guid projectId) =>
        await db.Projects.AnyAsync(p => p.Id == projectId
                                     && p.Organization.TenantId == ctx.CurrentTenant!.Id);
}

// ──────────────────────────────────────────────────────────────────────────
// Response records
// ──────────────────────────────────────────────────────────────────────────

public record ImportTrxResponse(
    Guid RunId,
    Guid SuiteId,
    int TotalTests,
    int PassedTests,
    int FailedTests,
    int SkippedTests,
    double DurationMs);

public record ImportCoverageResponse(
    Guid RunId,
    Guid ReportId,
    double LineRate,
    double BranchRate,
    int LinesCovered,
    int LinesValid,
    int BranchesCovered,
    int BranchesValid);
