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
    IHubContext<ProjectHub> projectHub) : ControllerBase
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
                r.WorkspacePath,
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
                r.CommitSha,
                r.Branch,
                r.Workflow,
                r.Status,
                StatusName = r.Status.ToString(),
                r.StartedAt,
                r.EndedAt,
                r.ExternalSource,
                r.ExternalRunId,
                r.WorkspacePath,
            })
            .FirstOrDefaultAsync();

        return run is null ? NotFound() : Ok(run);
    }

    [HttpGet("{id:guid}/logs")]
    public async Task<IActionResult> GetLogs(Guid id, [FromQuery] LogStream? stream)
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

        var logs = await query
            .Select(l => new { l.Id, l.Line, l.Stream, StreamName = l.Stream.ToString(), l.JobId, l.Timestamp })
            .ToListAsync();

        return Ok(logs);
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

        // Auto-detect workflow file when the run doesn't have an explicit workflow path stored.
        var workflow = run.Workflow;
        if (string.IsNullOrWhiteSpace(workflow))
        {
            var workflowsDir = Path.Combine(run.WorkspacePath, ".github", "workflows");
            if (Directory.Exists(workflowsDir))
            {
                workflow = Directory
                    .EnumerateFiles(workflowsDir, "*.yml")
                    .Concat(Directory.EnumerateFiles(workflowsDir, "*.yaml"))
                    .Select(Path.GetFileName)
                    .FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(workflow))
                return NotFound(new { error = "No workflow file found in workspace '.github/workflows/'." });
        }

        var yamlPath = TryFindWorkflowYamlPath(run.WorkspacePath, workflow);

        if (yamlPath is null)
            return NotFound(new { error = $"Workflow file '{workflow}' not found in workspace '{run.WorkspacePath}'." });

        var graph = await WorkflowGraphParser.ParseFileAsync(yamlPath, HttpContext.RequestAborted);

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
    /// Returns logs for a specific job within a run, identified by job name/id.
    /// </summary>
    [HttpGet("{id:guid}/jobs/{jobId}/logs")]
    public async Task<IActionResult> GetJobLogs(Guid id, string jobId, [FromQuery] LogStream? stream)
    {
        var runExists = await db.CiCdRuns
            .AnyAsync(r => r.Id == id && r.Project.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (!runExists) return NotFound();

        var query = db.CiCdRunLogs
            .Where(l => l.CiCdRunId == id && l.JobId == jobId)
            .OrderBy(l => l.Timestamp)
            .AsQueryable();

        if (stream.HasValue)
            query = query.Where(l => l.Stream == stream.Value);

        var logs = await query
            .Select(l => new { l.Id, l.Line, l.Stream, StreamName = l.Stream.ToString(), l.JobId, l.Timestamp })
            .ToListAsync();

        return Ok(logs);
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

        if (run.Status is not (CiCdRunStatus.Failed or CiCdRunStatus.Cancelled))
            return Conflict(new { error = "Only failed or cancelled runs can be retried.", run.Status, StatusName = run.Status.ToString() });

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
                return Conflict(new
                {
                    error = "Another run is already in progress for this project.",
                    activeRunId = activeRun.Id,
                    activeRunStatus = activeRun.StatusName,
                    canForce = true,
                });
        }

        // Publish a new trigger — the CiCdWorker will create a new run record.
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            projectId = run.ProjectId,
            commitSha = run.CommitSha,
            branch = run.Branch,
            workflow = run.Workflow,
            agentSessionId = run.AgentSessionId,
            workspacePath = run.WorkspacePath,
            eventName = "push",
            keepContainerOnFailure = options?.KeepContainerOnFailure ?? false,
            noDind = options?.NoDind ?? false,
            noVolumeMounts = options?.NoVolumeMounts ?? false,
            customImage = options?.CustomImage,
            customEntrypoint = options?.CustomEntrypoint,
            customArgs = options?.CustomArgs,
        });

        await producer.ProduceAsync("cicd-trigger", new Message<string, string>
        {
            Key = run.CommitSha,
            Value = payload,
        });

        return Accepted(new { retriedRunId = run.Id });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelRun(Guid id)
    {
        var run = await db.CiCdRuns
            .Include(r => r.Project)
            .FirstOrDefaultAsync(r => r.Id == id && r.Project.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (run is null) return NotFound();

        if (run.Status is not (CiCdRunStatus.Pending or CiCdRunStatus.Running))
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
    /// <summary>When true the Docker socket is NOT mounted (disables DinD).</summary>
    bool NoDind = false,
    /// <summary>When true no host volumes are mounted (workspace and docker socket omitted).</summary>
    bool NoVolumeMounts = false,
    /// <summary>Override the Docker image for this run.</summary>
    string? CustomImage = null,
    /// <summary>Override the container entrypoint.</summary>
    string? CustomEntrypoint = null,
    /// <summary>Additional CLI arguments appended to the act command.</summary>
    string? CustomArgs = null);

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
