using Confluent.Kafka;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/agent-sessions")]
public class AgentSessionsController(IssuePitDbContext db, TenantContext tenant, IProducer<string, string> producer) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSession(Guid id)
    {
        var session = await db.AgentSessions
            .Include(s => s.Agent)
            .Include(s => s.Issue).ThenInclude(i => i.Project)
            .Include(s => s.CiCdRuns)
            .Where(s => s.Id == id && s.Issue.Project!.Organization.TenantId == tenant.CurrentTenant!.Id)
            .Select(s => new
            {
                s.Id,
                s.AgentId,
                AgentName = s.Agent.Name,
                s.IssueId,
                IssueTitle = s.Issue.Title,
                IssueNumber = s.Issue.Number,
                ProjectId = s.Issue.ProjectId,
                ProjectName = s.Issue.Project!.Name,
                s.CommitSha,
                s.GitBranch,
                s.Status,
                StatusName = s.Status.ToString(),
                s.StartedAt,
                s.EndedAt,
                s.Warnings,
                CiCdRuns = s.CiCdRuns.Select(r => new
                {
                    r.Id,
                    r.Status,
                    StatusName = r.Status.ToString(),
                    r.Workflow,
                    r.Branch,
                    r.CommitSha,
                    r.StartedAt,
                    r.EndedAt,
                    r.ExternalSource,
                    r.ExternalRunId,
                }),
            })
            .FirstOrDefaultAsync();

        return session is null ? NotFound() : Ok(session);
    }

    [HttpGet("{id:guid}/logs")]
    public async Task<IActionResult> GetSessionLogs(Guid id)
    {
        var sessionExists = await db.AgentSessions
            .AnyAsync(s => s.Id == id && s.Issue.Project!.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (!sessionExists) return NotFound();

        var logs = await db.AgentSessionLogs
            .Where(l => l.AgentSessionId == id)
            .OrderBy(l => l.Timestamp)
            .Select(l => new
            {
                l.Id,
                l.Line,
                Stream = l.Stream.ToString().ToLower(),
                StreamName = l.Stream.ToString(),
                l.Timestamp,
            })
            .ToListAsync();

        return Ok(logs);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelSession(Guid id)
    {
        var session = await db.AgentSessions
            .Include(s => s.Issue).ThenInclude(i => i.Project)
            .FirstOrDefaultAsync(s => s.Id == id && s.Issue.Project!.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (session is null) return NotFound();

        if (session.Status is not (AgentSessionStatus.Pending or AgentSessionStatus.Running))
            return Conflict(new { error = "Only pending or running sessions can be cancelled.", session.Status, StatusName = session.Status.ToString() });

        // Publish a cancel signal to the ExecutionClient. The worker will cancel the session
        // and update the status to Cancelled via its IssueWorker.RunCancelConsumerAsync handler.
        await producer.ProduceAsync("agent-cancel", new Message<string, string>
        {
            Key = session.Id.ToString(),
            Value = session.Id.ToString(),
        });

        return Accepted(new { session.Id, Status = session.Status, StatusName = session.Status.ToString() });
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> RetrySession(Guid id, [FromBody] RetrySessionRequest? body = null)
    {
        var session = await db.AgentSessions
            .Include(s => s.Agent)
            .Include(s => s.Issue).ThenInclude(i => i.Project)
            .FirstOrDefaultAsync(s => s.Id == id && s.Issue.Project!.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (session is null) return NotFound();

        if (session.Status is not (AgentSessionStatus.Failed or AgentSessionStatus.Cancelled))
            return Conflict(new { error = "Only failed or cancelled sessions can be retried.", session.Status, StatusName = session.Status.ToString() });

        // Re-publish issue-assigned so the ExecutionClient creates a new session for the same agent and issue.
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            id = session.IssueId,
            projectId = session.Issue.ProjectId,
            title = session.Issue.Title,
            agentId = session.AgentId,
            dockerImageOverride = body?.DockerImageOverride,
            keepContainer = body?.KeepContainer ?? false,
        });

        await producer.ProduceAsync("issue-assigned", new Message<string, string>
        {
            Key = session.IssueId.ToString(),
            Value = payload,
        });

        return Accepted(new { retriedSessionId = session.Id });
    }
}

public record RetrySessionRequest(string? DockerImageOverride = null, bool KeepContainer = false);
