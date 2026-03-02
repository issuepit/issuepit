using Confluent.Kafka;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/cicd-runs")]
public class CiCdRunsController(IssuePitDbContext db, TenantContext tenant, IProducer<string, string> producer) : ControllerBase
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
            .Select(l => new { l.Id, l.Line, l.Stream, StreamName = l.Stream.ToString(), l.Timestamp })
            .ToListAsync();

        return Ok(logs);
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
}

/// <summary>Options body for the retry run endpoint.</summary>
public record RetryRunOptions(
    /// <summary>When true the Docker container is not removed after a failed run, for debugging.</summary>
    bool KeepContainerOnFailure = false,
    /// <summary>When true the retry proceeds even if another run for the same project is already in progress.</summary>
    bool ForceRetry = false);

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
