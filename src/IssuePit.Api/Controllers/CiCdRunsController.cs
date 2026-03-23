using Confluent.Kafka;
using IssuePit.Api.Hubs;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/cicd-runs")]
public class CiCdRunsController(
    IssuePitDbContext db,
    TenantContext tenant,
    IProducer<string, string> producer,
    IHubContext<ProjectHub> projectHub,
    CiCdRunQueueService runQueue,
    ImageStorageService imageStorage,
    GitService gitService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRuns([FromQuery] Guid? projectId)
    {
        var query = db.CiCdRuns
            .Include(r => r.Project)
            .Where(r => r.Project.Organization.TenantId == tenant.CurrentTenant!.Id)
            .OrderByDescending(r => r.StartedAt)
            .AsQueryable();

        if (projectId.HasValue)
            query = query.Where(r => r.ProjectId == projectId.Value);

        var runs = await query
            .Select(r => new
            {
                r.Id,
                r.ProjectId,
                ProjectName = r.Project.Name,
                r.AgentSessionId,
                r.CommitSha,
                r.Branch,
                r.Workflow,
                r.Status,
                StatusName = r.Status.ToString(),
                r.StartedAt,
                r.EndedAt,
                r.ExternalSource,
                r.ExternalRunId,
                r.ExternalRunUrl,
                r.WorkspacePath,
                r.EventName,
                r.InputsJson,
                r.SkipSteps,
            })
            .Take(100)
            .ToListAsync();

        return Ok(runs);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRun(Guid id)
    {
        var run = await db.CiCdRuns
            .Include(r => r.Project)
            .Where(r => r.Id == id && r.Project.Organization.TenantId == tenant.CurrentTenant!.Id)
            .Select(r => new
            {
                r.Id,
                r.ProjectId,
                r.AgentSessionId,
                r.RetryOfRunId,
                r.CommitSha,
                r.Branch,
                r.Workflow,
                r.Status,
                StatusName = r.Status.ToString(),
                r.StartedAt,
                r.EndedAt,
                r.ExternalSource,
                r.ExternalRunId,
                r.ExternalRunUrl,
                r.WorkspacePath,
                r.EventName,
                r.InputsJson,
                r.SkipSteps,
            })
            .FirstOrDefaultAsync();

        return run is null ? NotFound() : Ok(run);
    }

    /// <summary>
    /// Returns runs that are related to the given run:
    /// retries of the same original run, the run that was retried to produce this run,
    /// the agent session that triggered this run, and other runs on the same commit SHA.
    /// </summary>
    [HttpGet("{id:guid}/linked")]
    public async Task<IActionResult> GetLinkedRuns(Guid id)
    {
        var run = await db.CiCdRuns
            .Include(r => r.Project)
            .Where(r => r.Id == id && r.Project.Organization.TenantId == tenant.CurrentTenant!.Id)
            .Select(r => new { r.Id, r.ProjectId, r.CommitSha, r.AgentSessionId, r.RetryOfRunId })
            .FirstOrDefaultAsync();

        if (run is null) return NotFound();

        var results = new List<object>();

        // 1. The original run that was retried to produce this one.
        if (run.RetryOfRunId.HasValue)
        {
            var original = await db.CiCdRuns
                .Where(r => r.Id == run.RetryOfRunId.Value && r.ProjectId == run.ProjectId)
                .Select(r => new
                {
                    r.Id,
                    r.ProjectId,
                    r.CommitSha,
                    r.Branch,
                    r.Workflow,
                    r.Status,
                    StatusName = r.Status.ToString(),
                    r.StartedAt,
                    r.EndedAt,
                    LinkType = "retry-of",
                    LinkLabel = "Retried from",
                })
                .FirstOrDefaultAsync();
            if (original is not null)
                results.Add(original);
        }

        // 2. Runs that are retries of this run (direct children).
        var retries = await db.CiCdRuns
            .Where(r => r.RetryOfRunId == id && r.ProjectId == run.ProjectId)
            .OrderBy(r => r.StartedAt)
            .Select(r => new
            {
                r.Id,
                r.ProjectId,
                r.CommitSha,
                r.Branch,
                r.Workflow,
                r.Status,
                StatusName = r.Status.ToString(),
                r.StartedAt,
                r.EndedAt,
                LinkType = "retry",
                LinkLabel = "Retry",
            })
            .ToListAsync();
        results.AddRange(retries.Cast<object>());

        // 3. Agent session that triggered this run.
        if (run.AgentSessionId.HasValue)
        {
            var session = await db.AgentSessions
                .Include(s => s.Issue)
                .Where(s => s.Id == run.AgentSessionId.Value)
                .Select(s => new
                {
                    s.Id,
                    ProjectId = run.ProjectId,
                    IssueTitle = s.Issue.Title,
                    IssueNumber = s.Issue.Number,
                    s.CommitSha,
                    GitBranch = s.GitBranch,
                    s.Status,
                    StatusName = s.Status.ToString(),
                    s.StartedAt,
                    s.EndedAt,
                    LinkType = "agent-triggered",
                    LinkLabel = "Agent Session",
                })
                .FirstOrDefaultAsync();
            if (session is not null)
                results.Add(session);
        }

        // 4. Other runs on the same commit SHA (excluding this run and already-listed retries).
        var alreadyListed = retries.Select(r => r.Id).Append(run.RetryOfRunId ?? Guid.Empty).ToHashSet();
        var sameCommit = await db.CiCdRuns
            .Where(r => r.CommitSha == run.CommitSha
                && r.ProjectId == run.ProjectId
                && r.Id != id
                && !alreadyListed.Contains(r.Id))
            .OrderByDescending(r => r.StartedAt)
            .Take(10)
            .Select(r => new
            {
                r.Id,
                r.ProjectId,
                r.CommitSha,
                r.Branch,
                r.Workflow,
                r.Status,
                StatusName = r.Status.ToString(),
                r.StartedAt,
                r.EndedAt,
                LinkType = "same-sha",
                LinkLabel = "Same commit",
            })
            .ToListAsync();
        results.AddRange(sameCommit.Cast<object>());

        return Ok(results);
    }

    [HttpGet("{id:guid}/logs")]
    public async Task<IActionResult> GetLogs(Guid id, [FromQuery] LogStream? stream, [FromQuery] string? jobId)
    {
        // Verify the run belongs to this tenant
        var runExists = await db.CiCdRuns
            .AnyAsync(r => r.Id == id && r.Project.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (!runExists) return NotFound();

        var query = db.CiCdRunLogs
            .Where(l => l.CiCdRunId == id)
            .OrderBy(l => l.Timestamp)
            .AsQueryable();

        if (stream.HasValue)
            query = query.Where(l => l.Stream == stream.Value);

        // Filter by the exact stored jobId (act's qualified name, e.g. "CI/build").
        if (!string.IsNullOrEmpty(jobId))
            query = query.Where(l => l.JobId == jobId);

        var logs = await query
            .Select(l => new { l.Id, l.Line, l.Stream, StreamName = l.Stream.ToString(), l.JobId, l.StepId, l.Timestamp })
            .ToListAsync();

        return Ok(logs);
    }

    /// <summary>
    /// Returns parsed test results (test suites and individual test cases) for the given run.
    /// Test results are collected automatically from artifact <c>.trx</c> files after the run completes.
    /// </summary>
    [HttpGet("{id:guid}/test-results")]
    public async Task<IActionResult> GetTestResults(Guid id)
    {
        var runExists = await db.CiCdRuns
            .AnyAsync(r => r.Id == id && r.Project.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (!runExists) return NotFound();

        var suites = await db.CiCdTestSuites
            .Where(s => s.CiCdRunId == id)
            .OrderBy(s => s.CreatedAt)
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
                TestCases = s.TestCases
                    .OrderBy(tc => tc.FullName)
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
                    })
                    .ToList(),
            })
            .ToListAsync();

        return Ok(suites);
    }

    /// <summary>
    /// Returns artifacts produced by the given run (name, size, file count, storage key).
    /// </summary>
    [HttpGet("{id:guid}/artifacts")]
    public async Task<IActionResult> GetArtifacts(Guid id)
    {
        var runExists = await db.CiCdRuns
            .AnyAsync(r => r.Id == id && r.Project.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (!runExists) return NotFound();

        var artifacts = await db.CiCdArtifacts
            .Where(a => a.CiCdRunId == id)
            .OrderBy(a => a.Name)
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.SizeBytes,
                a.FileCount,
                a.StorageKey,
                a.IsTestResultArtifact,
                a.CreatedAt,
            })
            .ToListAsync();

        return Ok(artifacts);
    }

    /// <summary>
    /// Downloads the artifact ZIP by proxying the S3 object through the backend.
    /// Returns 503 when artifact storage is not configured.
    /// </summary>
    [HttpGet("{id:guid}/artifacts/{artifactId:guid}/download")]
    public async Task<IActionResult> DownloadArtifact(Guid id, Guid artifactId, CancellationToken ct)
    {
        if (!imageStorage.IsConfigured)
            return StatusCode(503, new { error = "Artifact storage is not configured." });

        var artifact = await db.CiCdArtifacts
            .Where(a => a.Id == artifactId
                        && a.CiCdRunId == id
                        && a.CiCdRun.Project.Organization.TenantId == tenant.CurrentTenant!.Id)
            .Select(a => new { a.Name, a.StorageKey })
            .FirstOrDefaultAsync(ct);

        if (artifact is null) return NotFound();
        if (string.IsNullOrEmpty(artifact.StorageKey))
            return StatusCode(404, new { error = "Artifact has not been uploaded to storage." });

        try
        {
            var (stream, contentType) = await imageStorage.OpenDownloadStreamAsync(artifact.StorageKey, ct);
            var fileName = $"{artifact.Name}.zip";
            return File(stream, contentType, fileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { error = "Artifact file not found in storage." });
        }
    }

    /// <summary>
    /// Returns the workflow job graph (nodes and dependency edges) for the given run.
    /// First returns the pre-computed graph stored in the DB (if available), then falls back to
    /// parsing the workflow YAML from the workspace. Returns 404 when no graph data can be found.
    /// </summary>
    [HttpGet("{id:guid}/graph")]
    public async Task<IActionResult> GetGraph(Guid id)
    {
        var run = await db.CiCdRuns
            .Include(r => r.Project)
            .Where(r => r.Id == id && r.Project.Organization.TenantId == tenant.CurrentTenant!.Id)
            .Select(r => new { r.WorkspacePath, r.Workflow, r.WorkflowGraphJson })
            .FirstOrDefaultAsync();

        if (run is null) return NotFound();

        // Return pre-computed graph if available (written by the worker at run start or exec step).
        if (!string.IsNullOrWhiteSpace(run.WorkflowGraphJson))
        {
            try
            {
                var cached = System.Text.Json.JsonSerializer.Deserialize<WorkflowGraph>(
                    run.WorkflowGraphJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (cached is not null)
                    return Ok(cached);
            }
            catch { /* fall through to filesystem parse */ }
        }

        if (string.IsNullOrWhiteSpace(run.WorkspacePath))
            return NotFound(new { error = "This run has no local workspace. Graph data is only available for locally-triggered runs." });

        var workflowsDir = Path.Combine(run.WorkspacePath, ".github", "workflows");

        WorkflowGraph graph;
        if (!string.IsNullOrWhiteSpace(run.Workflow))
        {
            // Run was triggered for a specific workflow file — parse only that file.
            var yamlPath = TryFindWorkflowYamlPath(run.WorkspacePath, run.Workflow);
            if (yamlPath is null)
                return NotFound(new { error = $"Workflow file '{run.Workflow}' not found in workspace '{run.WorkspacePath}'." });

            graph = await WorkflowGraphParser.ParseFileAsync(yamlPath, HttpContext.RequestAborted);
        }
        else if (Directory.Exists(workflowsDir))
        {
            // No specific workflow — merge all workflow files in .github/workflows/.
            graph = await WorkflowGraphParser.ParseDirectoryAsync(workflowsDir, HttpContext.RequestAborted);
            if (graph.Jobs.Count == 0)
                return NotFound(new { error = "No workflow files with jobs found in workspace '.github/workflows/'." });
        }
        else
        {
            return NotFound(new { error = "No '.github/workflows/' directory found in workspace." });
        }

        // Cache the parsed graph in the DB so future requests don't need the workspace.
        try
        {
            var runToUpdate = await db.CiCdRuns.FindAsync(id);
            if (runToUpdate is not null)
            {
                runToUpdate.WorkflowGraphJson = System.Text.Json.JsonSerializer.Serialize(graph);
                await db.SaveChangesAsync(HttpContext.RequestAborted);
            }
        }
        catch { /* best-effort — caching failure must not fail the response */ }

        return Ok(graph);
    }

    /// <summary>
    /// Returns a compact job-status summary for every job that produced log output in this run.
    /// Status is derived purely from log analysis: a job is "succeeded" when it emits
    /// "Job succeeded", "failed" when it emits "Job failed", and "running" otherwise.
    /// Used by the frontend mini-graph tooltip to colour job nodes.
    /// </summary>
    [HttpGet("{id:guid}/job-statuses")]
    public async Task<IActionResult> GetJobStatuses(Guid id)
    {
        var run = await db.CiCdRuns
            .Where(r => r.Id == id && r.Project.Organization.TenantId == tenant.CurrentTenant!.Id)
            .Select(r => new { r.Status })
            .FirstOrDefaultAsync();

        if (run == null) return NotFound();

        // A terminal run is one that has finished (success/failure/cancelled).
        // Jobs that have no success/failure log lines in a terminal run must have timed out or been
        // cancelled, so we surface them as "failed" rather than the misleading "running" status.
        var isTerminal = run.Status is not (CiCdRunStatus.Running or CiCdRunStatus.Pending or CiCdRunStatus.WaitingForApproval);

        var jobGroups = await db.CiCdRunLogs
            .Where(l => l.CiCdRunId == id && l.JobId != null)
            .GroupBy(l => l.JobId!)
            .Select(g => new
            {
                LogJobId = g.Key,
                HasJobSucceeded = g.Any(l => l.Line.EndsWith("Job succeeded")),
                HasJobFailed = g.Any(l => l.Line.EndsWith("Job failed")),
                StartedAt = g.Min(l => l.Timestamp),
                EndedAt = g.Max(l => l.Timestamp),
            })
            .ToListAsync();

        var result = jobGroups.Select(g =>
        {
            string status;
            if (g.HasJobFailed) status = "failed";
            else if (g.HasJobSucceeded) status = "succeeded";
            else if (isTerminal) status = "failed";
            else status = "running";
            return new CiCdJobStatusDto(g.LogJobId, status, g.StartedAt, g.EndedAt);
        });

        return Ok(result);
    }

    /// <summary>
    /// Returns logs for a specific job within a run, identified by job name/id.
    /// Optionally filtered by stream and/or step name.
    /// </summary>
    [HttpGet("{id:guid}/jobs/{jobId}/logs")]
    public async Task<IActionResult> GetJobLogs(Guid id, string jobId, [FromQuery] LogStream? stream, [FromQuery] string? step)
    {
        var runExists = await db.CiCdRuns
            .AnyAsync(r => r.Id == id && r.Project.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (!runExists) return NotFound();

        // Match exact JobId OR a workflow-qualified name ending with "/<jobId>" (e.g. "CI/build" for jobId="build").
        // The act `job` field stores the workflow-qualified name (e.g. "CI/build"); callers may pass the
        // plain YAML key ("build") which is the trailing segment after the last "/".
        var suffix = "/" + jobId;
        var query = db.CiCdRunLogs
            .Where(l => l.CiCdRunId == id && (l.JobId == jobId || l.JobId!.EndsWith(suffix)))
            .OrderBy(l => l.Timestamp)
            .AsQueryable();

        if (stream.HasValue)
            query = query.Where(l => l.Stream == stream.Value);

        if (!string.IsNullOrEmpty(step))
            query = query.Where(l => l.StepId == step);

        var logs = await query
            .Select(l => new { l.Id, l.Line, l.Stream, StreamName = l.Stream.ToString(), l.JobId, l.StepId, l.Timestamp })
            .ToListAsync();

        return Ok(logs);
    }

    /// <summary>
    /// Returns the distinct step names (act <c>stage</c> values) for a specific job within a run,
    /// in the order they first appeared.
    /// </summary>
    [HttpGet("{id:guid}/jobs/{jobId}/steps")]
    public async Task<IActionResult> GetJobSteps(Guid id, string jobId)
    {
        var runExists = await db.CiCdRuns
            .AnyAsync(r => r.Id == id && r.Project.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (!runExists) return NotFound();

        // Match exact JobId OR a workflow-qualified name ending with "/<jobId>" (same as GetJobLogs).
        var suffix = "/" + jobId;
        var steps = await db.CiCdRunLogs
            .Where(l => l.CiCdRunId == id && (l.JobId == jobId || l.JobId!.EndsWith(suffix)) && l.StepId != null)
            .GroupBy(l => l.StepId!)
            .Select(g => new { StepId = g.Key, FirstSeen = g.Min(l => l.Timestamp) })
            .OrderBy(s => s.FirstSeen)
            .Select(s => s.StepId)
            .ToListAsync();

        return Ok(steps);
    }

    /// <summary>
    /// Returns distinct job/step combinations seen in recent runs for a project.
    /// Used by the wizard UI to provide autocomplete suggestions for skip-step configuration.
    /// </summary>
    [HttpGet("step-suggestions")]
    public async Task<IActionResult> GetStepSuggestions([FromQuery] Guid? projectId)
    {
        if (projectId is null) return BadRequest("projectId is required.");

        var projectExists = await db.Projects
            .AnyAsync(p => p.Id == projectId && p.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (!projectExists) return NotFound();

        // Take the 20 most recent completed runs for the project.
        var recentRunIds = await db.CiCdRuns
            .Where(r => r.ProjectId == projectId &&
                        (r.Status == CiCdRunStatus.Succeeded || r.Status == CiCdRunStatus.Failed))
            .OrderByDescending(r => r.StartedAt)
            .Select(r => r.Id)
            .Take(20)
            .ToListAsync();

        if (recentRunIds.Count == 0)
            return Ok(Array.Empty<object>());

        // Collect distinct job+step combinations from those runs, with their first-seen timestamp.
        var pairs = await db.CiCdRunLogs
            .Where(l => recentRunIds.Contains(l.CiCdRunId) && l.JobId != null && l.StepId != null)
            .GroupBy(l => new { l.JobId, l.StepId })
            .Select(g => new { g.Key.JobId, g.Key.StepId, FirstSeen = g.Min(l => l.Timestamp) })
            .ToListAsync();

        // Normalise job IDs: act stores them as "WorkflowName/JobId" — use only the trailing part.
        // Re-group after normalisation in case multiple raw job IDs collapse to the same short name.
        var normalised = pairs
            .Select(p => new
            {
                JobId = p.JobId!.Contains('/') ? p.JobId[(p.JobId.LastIndexOf('/') + 1)..] : p.JobId,
                StepId = p.StepId!,
                p.FirstSeen,
            })
            .GroupBy(p => new { p.JobId, p.StepId })
            .Select(g => new { g.Key.JobId, g.Key.StepId, FirstSeen = g.Min(p => p.FirstSeen) })
            .ToList();

        var result = normalised
            .GroupBy(p => p.JobId)
            .OrderBy(g => g.Min(p => p.FirstSeen))
            .Select(g => new StepSuggestionJobDto(
                g.Key,
                g.OrderBy(p => p.FirstSeen).Select(p => p.StepId).ToList()))
            .ToList();

        return Ok(result);
    }

    private static string? TryFindWorkflowYamlPath(string workspacePath, string workflow)
    {
        // Resolve the workspace to a canonical path so we can detect path traversal attempts.
        var canonicalWorkspace = Path.GetFullPath(workspacePath);

        // Build candidates using only the filename component to prevent path traversal via
        // the workflow value (e.g. "../../etc/passwd" stored by a user or external source).
        var workflowFileName = Path.GetFileName(workflow);

        // Also allow a well-known relative sub-path: ".github/workflows/<filename>"
        // Only use the workflow value as-is when it is a simple filename (no directory separators),
        // or when it resolves to a path still within the workspace.
        var candidates = new List<string>
        {
            Path.Combine(canonicalWorkspace, ".github", "workflows", workflowFileName),
        };

        // If workflow has no directory component (just a filename), also try directly under workspace.
        if (workflowFileName == workflow || workflowFileName == Path.GetFileName(workflow.Replace('/', Path.DirectorySeparatorChar)))
        {
            candidates.Insert(0, Path.Combine(canonicalWorkspace, workflow.TrimStart('/').TrimStart('\\').Replace('/', Path.DirectorySeparatorChar)));
        }

        foreach (var path in candidates)
        {
            try
            {
                // Safety check: ensure the resolved path is still under the workspace root.
                var resolvedPath = Path.GetFullPath(path);
                if (!resolvedPath.StartsWith(canonicalWorkspace, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (System.IO.File.Exists(resolvedPath))
                    return resolvedPath;
            }
            catch
            {
                // best-effort
            }
        }

        return null;
    }

    // Accepts state updates pushed by external CI/CD systems (e.g. GitHub Actions webhooks).
    // Creates a new run record or updates an existing one matched by (projectId, externalSource, externalRunId).
    [HttpPost("external-sync")]
    public async Task<IActionResult> ExternalSync([FromBody] ExternalSyncRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest("projectId is required");

        if (string.IsNullOrWhiteSpace(request.ExternalSource))
            return BadRequest("externalSource is required");

        if (string.IsNullOrWhiteSpace(request.ExternalRunId))
            return BadRequest("externalRunId is required");

        // Ensure the project belongs to this tenant.
        // Return NotFound (not Unauthorized) to avoid leaking whether a project exists across tenant boundaries.
        var projectExists = await db.Projects
            .AnyAsync(p => p.Id == request.ProjectId && p.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (!projectExists) return NotFound();

        var status = MapExternalStatus(request.Status, request.Conclusion);

        // Try to find an existing run for this external source + run ID
        var run = await db.CiCdRuns
            .FirstOrDefaultAsync(r =>
                r.ProjectId == request.ProjectId &&
                r.ExternalSource == request.ExternalSource &&
                r.ExternalRunId == request.ExternalRunId);

        if (run is null)
        {
            run = new CiCdRun
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                CommitSha = request.CommitSha ?? string.Empty,
                Branch = request.Branch,
                Workflow = request.Workflow,
                ExternalSource = request.ExternalSource,
                ExternalRunId = request.ExternalRunId,
                Status = status,
                StartedAt = request.StartedAt ?? DateTime.UtcNow,
                EndedAt = request.EndedAt,
            };
            db.CiCdRuns.Add(run);
        }
        else
        {
            run.Status = status;
            if (!string.IsNullOrWhiteSpace(request.CommitSha))
                run.CommitSha = request.CommitSha;
            if (!string.IsNullOrWhiteSpace(request.Branch))
                run.Branch = request.Branch;
            if (!string.IsNullOrWhiteSpace(request.Workflow))
                run.Workflow = request.Workflow;
            if (request.EndedAt.HasValue)
                run.EndedAt = request.EndedAt;
        }

        await db.SaveChangesAsync();

        await NotifyRunsUpdated(run);

        return Ok(new { run.Id, run.Status, StatusName = run.Status.ToString() });
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> RetryRun(Guid id, [FromBody] RetryRunOptions? options = null)
    {
        var run = await db.CiCdRuns
            .Include(r => r.Project)
            .FirstOrDefaultAsync(r => r.Id == id && r.Project.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (run is null) return NotFound();

        // Only terminal runs can be retried/retriggered; in-progress or pending runs should be
        // cancelled first to avoid duplicate execution.
        if (run.Status is CiCdRunStatus.Pending or CiCdRunStatus.Running or CiCdRunStatus.WaitingForApproval)
            return Conflict(new { error = "Run is still in progress. Cancel it before retriggering.", run.Status, StatusName = run.Status.ToString() });

        // Warn if another run for the same project is already in progress, unless the caller forces it.
        if (options?.ForceRetry != true)
        {
            var activeRun = await db.CiCdRuns
                .Where(r => r.ProjectId == run.ProjectId
                    && r.Id != run.Id
                    && (r.Status == CiCdRunStatus.Running || r.Status == CiCdRunStatus.Pending))
                .Select(r => new { r.Id, StatusName = r.Status.ToString() })
                .FirstOrDefaultAsync();

            if (activeRun is not null)
                return Conflict(new ActiveRunConflictResponse(
                    Error: "Another run is already in progress for this project.",
                    ActiveRunId: activeRun.Id,
                    ActiveRunStatus: activeRun.StatusName,
                    CanForce: true));
        }

        // Re-resolve the remote URL so the container can clone the latest state of the repo.
        var retryRepo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == run.ProjectId);

        // Determine effective branch and commit SHA, honouring caller overrides.
        var retryBranch = !string.IsNullOrWhiteSpace(options?.Branch) ? options.Branch : run.Branch;
        string retryCommitSha;
        if (!string.IsNullOrWhiteSpace(options?.CommitSha))
        {
            retryCommitSha = options.CommitSha;
        }
        else if (!string.IsNullOrWhiteSpace(options?.Branch))
        {
            // Branch overridden without an explicit SHA — resolve the branch tip from the local clone.
            retryCommitSha = (retryRepo is not null ? gitService.GetBranchTipSha(retryRepo, options.Branch) : null)
                ?? options.Branch;
        }
        else
        {
            retryCommitSha = run.CommitSha;
        }

        // Create the new run record immediately (Pending) so it shows as queued in the UI.
        // Retries are user-initiated so they bypass RequiresRunApproval.
        // Inherit skip steps from original run unless explicitly overridden by the caller.
        var retrySkipSteps = options?.OverrideSkipSteps == true
            ? options.SkipSteps
            : (options?.SkipSteps ?? run.SkipSteps);
        var newRun = await runQueue.EnqueueAsync(
            projectId: run.ProjectId,
            commitSha: retryCommitSha,
            branch: retryBranch,
            workflow: run.Workflow,
            eventName: !string.IsNullOrWhiteSpace(options?.EventName) ? options.EventName : (run.EventName ?? "push"),
            inputs: null,
            gitRepoUrl: retryRepo?.RemoteUrl,
            agentSessionId: run.AgentSessionId,
            retryOfRunId: run.Id,
            extraPayload: new
            {
                keepContainerOnFailure = options?.KeepContainerOnFailure ?? false,
                noDind = options?.NoDind ?? false,
                noVolumeMounts = options?.NoVolumeMounts ?? false,
                customImage = options?.CustomImage,
                customEntrypoint = options?.CustomEntrypoint,
                customArgs = options?.CustomArgs,
                actRunnerImage = options?.ActRunnerImage,
                skipSteps = retrySkipSteps,
            },
            userTriggered: true);

        return Accepted(new { retriedRunId = newRun.Id });
    }

    /// <summary>
    /// Triggers a new CI/CD run for a specific commit and event type.
    /// Supports all GitHub Actions event types including push, pull_request, workflow_dispatch,
    /// workflow_call, merge_group, and release. For workflow_dispatch, optional inputs can be supplied.
    /// </summary>
    [HttpPost("trigger")]
    public async Task<IActionResult> TriggerRun([FromBody] TriggerRunRequest request)
    {
        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { error = "projectId is required" });

        if (string.IsNullOrWhiteSpace(request.CommitSha) && string.IsNullOrWhiteSpace(request.Branch))
            return BadRequest(new { error = "commitSha or branch is required" });

        if (string.IsNullOrWhiteSpace(request.EventName))
            return BadRequest(new { error = "eventName is required" });

        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (project is null) return NotFound();

        // Look up the remote URL from the linked git repository (if any).
        // The container clones the repo inside itself — no host workspace path is needed.
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == request.ProjectId);

        // When only a branch is given (no commit SHA), resolve the branch tip SHA from the local
        // clone so the run record has a meaningful commit identifier. Fall back to the branch name
        // itself when the local clone is unavailable.
        var commitSha = !string.IsNullOrWhiteSpace(request.CommitSha)
            ? request.CommitSha
            : (repo is not null ? gitService.GetBranchTipSha(repo, request.Branch!) : null) ?? request.Branch!;

        // Warn if another run for the same project is already in progress, unless the caller forces it.
        if (request.Force != true)
        {
            var activeRun = await db.CiCdRuns
                .Where(r => r.ProjectId == request.ProjectId
                    && (r.Status == CiCdRunStatus.Running || r.Status == CiCdRunStatus.Pending))
                .Select(r => new { r.Id, StatusName = r.Status.ToString() })
                .FirstOrDefaultAsync();

            if (activeRun is not null)
                return Conflict(new ActiveRunConflictResponse(
                    Error: "Another run is already in progress for this project.",
                    ActiveRunId: activeRun.Id,
                    ActiveRunStatus: activeRun.StatusName,
                    CanForce: true));
        }

        // Create the run record immediately (Pending) so it shows as queued in the UI.
        // Manual triggers are user-initiated so they bypass RequiresRunApproval.
        var newRun = await runQueue.EnqueueAsync(
            projectId: request.ProjectId,
            commitSha: commitSha,
            branch: request.Branch,
            workflow: request.Workflow,
            eventName: request.EventName,
            inputs: request.Inputs,
            gitRepoUrl: repo?.RemoteUrl,
            extraPayload: string.IsNullOrWhiteSpace(request.CustomImage) ? null : new { customImage = request.CustomImage },
            userTriggered: true);

        return Accepted(new { runId = newRun.Id, projectId = request.ProjectId, commitSha, eventName = request.EventName });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelRun(Guid id)
    {
        var run = await db.CiCdRuns
            .Include(r => r.Project)
            .FirstOrDefaultAsync(r => r.Id == id && r.Project.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (run is null) return NotFound();

        if (run.Status is not (CiCdRunStatus.Pending or CiCdRunStatus.Running or CiCdRunStatus.WaitingForApproval))
            return Conflict(new { error = "Run is already in a terminal state.", run.Status, StatusName = run.Status.ToString() });

        run.Status = CiCdRunStatus.Cancelled;
        run.EndedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Signal the CiCdClient worker to kill any in-flight execution for this run.
        await producer.ProduceAsync("cicd-cancel", new Message<string, string>
        {
            Key = run.Id.ToString(),
            Value = run.Id.ToString(),
        });

        await NotifyRunsUpdated(run);

        return Ok(new { run.Id, run.Status, StatusName = run.Status.ToString() });
    }

    /// <summary>
    /// Approves a CI/CD run that is in <c>WaitingForApproval</c> status, transitioning it to
    /// <c>Pending</c> and dispatching it to the CI/CD worker via Kafka.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveRun(Guid id)
    {
        // Verify the run belongs to the current tenant before approving.
        var runExists = await db.CiCdRuns
            .AnyAsync(r => r.Id == id && r.Project.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (!runExists) return NotFound();

        var approved = await runQueue.ApproveAsync(id);

        if (approved is null)
            return Conflict(new { error = "Run is not in WaitingForApproval state.", id });

        return Ok(new { approved.Id, approved.Status, StatusName = approved.Status.ToString() });
    }

    private static CiCdRunStatus MapExternalStatus(string? status, string? conclusion) =>
        status?.ToLowerInvariant() switch
        {
            "queued" => CiCdRunStatus.Pending,
            "in_progress" => CiCdRunStatus.Running,
            "completed" => conclusion?.ToLowerInvariant() switch
            {
                "success" => CiCdRunStatus.Succeeded,
                "cancelled" => CiCdRunStatus.Cancelled,
                _ => CiCdRunStatus.Failed,
            },
            _ => CiCdRunStatus.Pending,
        };

    private Task NotifyRunsUpdated(CiCdRun run) =>
        projectHub.Clients
            .Group(ProjectHub.ProjectGroup(run.ProjectId.ToString()))
            .SendAsync("RunsUpdated", new { runId = run.Id, status = run.Status, statusName = run.Status.ToString() });
}

/// <summary>Options body for the retry run endpoint.</summary>
public record RetryRunOptions(
    /// <summary>When true the Docker container is not removed after a failed run, for debugging.</summary>
    bool KeepContainerOnFailure = false,
    /// <summary>When true the retry proceeds even if another run for the same project is already in progress.</summary>
    bool ForceRetry = false,
    /// <summary>When true the Docker socket is NOT mounted into the container (disables Docker-in-Docker).</summary>
    bool NoDind = false,
    /// <summary>When true no host volumes are mounted into the container (workspace and docker socket are omitted).</summary>
    bool NoVolumeMounts = false,
    /// <summary>Override the Docker image used for the CI/CD container (the container that runs act). Null or empty = use configured default.</summary>
    string? CustomImage = null,
    /// <summary>Override the container entrypoint.</summary>
    string? CustomEntrypoint = null,
    /// <summary>Additional CLI arguments appended to the act command.</summary>
    string? CustomArgs = null,
    /// <summary>Override the act runner image used by act for platform mapping (e.g. ubuntu-latest). Null or empty = use project/org/global default.</summary>
    string? ActRunnerImage = null,
    /// <summary>Override the event/trigger name (e.g. "push", "pull_request"). Null or empty = use the original run's event name.</summary>
    string? EventName = null,
    /// <summary>Override the branch to run against. Null or empty = use the original run's branch.</summary>
    string? Branch = null,
    /// <summary>Override the commit SHA to run against. Null or empty = use the original run's commit SHA (or the branch tip when Branch is overridden).</summary>
    string? CommitSha = null,
    /// <summary>
    /// Override the skip-step configuration for this retry. Newline-separated step names or <c>job:step</c> pairs.
    /// When null the original run's skip steps are inherited. Pass an empty string to explicitly clear skip steps.
    /// </summary>
    string? SkipSteps = null,
    /// <summary>When true, the SkipSteps value (even if empty) explicitly overrides the inherited skip steps rather than falling back to the original run.</summary>
    bool OverrideSkipSteps = false);

/// <summary>Request body for the external CI/CD sync endpoint.</summary>
public record ExternalSyncRequest(
    Guid ProjectId,
    string ExternalSource,
    string ExternalRunId,
    string? CommitSha,
    string? Branch,
    string? Workflow,
    /// <summary>GitHub status values: queued | in_progress | completed</summary>
    string? Status,
    /// <summary>GitHub conclusion values: success | failure | cancelled | skipped | timed_out | action_required | neutral</summary>
    string? Conclusion,
    DateTime? StartedAt,
    DateTime? EndedAt);

/// <summary>Request body for the manual trigger endpoint.</summary>
public record TriggerRunRequest(
    Guid ProjectId,
    /// <summary>Commit SHA to run against. Either this or <see cref="Branch"/> must be provided.</summary>
    string? CommitSha,
    string EventName,
    /// <summary>Branch name. When <see cref="CommitSha"/> is omitted the branch tip is resolved automatically.</summary>
    string? Branch = null,
    string? Workflow = null,
    /// <summary>Input key-value pairs for workflow_dispatch events.</summary>
    Dictionary<string, string>? Inputs = null,
    /// <summary>Override the Docker image used for the CI/CD container (the container that runs act). Null or empty = use configured default.</summary>
    string? CustomImage = null,
    /// <summary>When true the trigger proceeds even if another run for the same project is already in progress.</summary>
    bool Force = false);

/// <summary>Conflict response returned when another run is already active for the project and the caller has not set the force flag.</summary>
public record ActiveRunConflictResponse(
    string Error,
    Guid ActiveRunId,
    string ActiveRunStatus,
    bool CanForce);


/// <summary>Per-job status derived from log analysis for the mini-graph tooltip.</summary>
public record CiCdJobStatusDto(
    /// <summary>The act log job ID (display name, e.g. "Build" or "Backend/Build").</summary>
    string LogJobId,
    /// <summary>One of: succeeded | failed | running.</summary>
    string Status,
    /// <summary>Timestamp of the first log entry for this job (job start time).</summary>
    DateTime? StartedAt = null,
    /// <summary>Timestamp of the last log entry for this job (job end time).</summary>
    DateTime? EndedAt = null);

/// <summary>Distinct steps seen for a single job in recent runs (used by the skip-step wizard).</summary>
public record StepSuggestionJobDto(
    /// <summary>The job ID as it appears in the workflow YAML.</summary>
    string JobId,
    /// <summary>Distinct step IDs (act stage values) seen for this job.</summary>
    IReadOnlyList<string> Steps);
