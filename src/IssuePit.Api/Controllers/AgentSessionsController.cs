using Confluent.Kafka;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/agent-sessions")]
public class AgentSessionsController(
    IssuePitDbContext db,
    TenantContext tenant,
    IProducer<string, string> producer,
    CiCdRunQueueService runQueue,
    GitService gitService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSession(Guid id)
    {
        var session = await db.AgentSessions
            .Include(s => s.Agent)
            .Include(s => s.Issue)
            .Include(s => s.Project).ThenInclude(p => p!.Organization)
            .Include(s => s.CiCdRuns)
            .Where(s => s.Id == id && s.Project!.Organization.TenantId == tenant.CurrentTenant!.Id)
            .Select(s => new
            {
                s.Id,
                s.AgentId,
                AgentName = s.Agent.Name,
                IsManualMode = s.Agent.ManualMode,
                s.IssueId,
                IssueTitle = s.Issue != null ? s.Issue.Title : null,
                IssueNumber = s.Issue != null ? (int?)s.Issue.Number : null,
                s.ProjectId,
                ProjectName = s.Project!.Name,
                s.CommitSha,
                s.GitBranch,
                s.Status,
                StatusName = s.Status.ToString(),
                s.StartedAt,
                s.EndedAt,
                s.Warnings,
                s.ServerWebUiUrl,
                s.OpenCodeSessionId,
                s.OpenCodeDbS3Url,
                s.GitRemoteCheckResultsJson,
                s.ContainerId,
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
            .AnyAsync(s => s.Id == id && s.Project!.Organization.TenantId == tenant.CurrentTenant!.Id);

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
                Section = l.Section != null ? l.Section.ToString() : null,
                l.SectionIndex,
            })
            .ToListAsync();

        return Ok(logs);
    }

    /// <summary>
    /// Returns a compact step summary for the agent session, derived from log section data.
    /// Each entry represents one distinct phase (section + sectionIndex) and indicates whether
    /// any log line in that phase contained an [ERROR] marker.
    /// </summary>
    [HttpGet("{id:guid}/steps")]
    public async Task<IActionResult> GetSessionSteps(Guid id)
    {
        var sessionExists = await db.AgentSessions
            .AnyAsync(s => s.Id == id && s.Issue.Project!.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (!sessionExists) return NotFound();

        var logs = await db.AgentSessionLogs
            .Where(l => l.AgentSessionId == id && l.Section != null)
            .OrderBy(l => l.Timestamp)
            .Select(l => new
            {
                l.Section,
                l.SectionIndex,
                l.Line,
                l.Timestamp,
            })
            .ToListAsync();

        // Group logs by (section, sectionIndex) in order of first appearance.
        // Track index in the groups list alongside the step so we can update in O(1).
        var groups = new List<AgentSessionStepDto>();
        var seen = new Dictionary<(string, int), (AgentSessionStepDto Step, int Index)>();

        foreach (var log in logs)
        {
            var section = log.Section!.Value.ToString();
            var key = (section, log.SectionIndex);
            if (!seen.TryGetValue(key, out var entry))
            {
                var step = new AgentSessionStepDto(
                    Section: section,
                    SectionIndex: log.SectionIndex,
                    Label: SectionLabel(log.Section!.Value, log.SectionIndex),
                    HasError: false,
                    StartedAt: log.Timestamp,
                    EndedAt: log.Timestamp);
                seen[key] = (step, groups.Count);
                groups.Add(step);
            }
            else
            {
                // Update endedAt and propagate error flag in O(1) using the tracked index.
                var updated = entry.Step with
                {
                    EndedAt = log.Timestamp,
                    HasError = entry.Step.HasError || log.Line.Contains("[ERROR]"),
                };
                seen[key] = (updated, entry.Index);
                groups[entry.Index] = updated;
            }
        }

        return Ok(groups);
    }

    private static string SectionLabel(IssuePit.Core.Enums.AgentLogSection section, int index) => section switch
    {
        IssuePit.Core.Enums.AgentLogSection.InitialAgentRun => "Initial Agent Run",
        IssuePit.Core.Enums.AgentLogSection.PostRun => "Post Run",
        IssuePit.Core.Enums.AgentLogSection.UncommittedChangesFix => "Uncommitted Changes Fix",
        IssuePit.Core.Enums.AgentLogSection.CiCdRun => $"CI/CD Run {index}",
        IssuePit.Core.Enums.AgentLogSection.CiCdFixRun => $"CI/CD Fix Run {index}",
        IssuePit.Core.Enums.AgentLogSection.MessageRun => $"Message {index}",
        _ => section.ToString(),
    };

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelSession(Guid id)
    {
        var session = await db.AgentSessions
            .Include(s => s.Project).ThenInclude(p => p!.Organization)
            .FirstOrDefaultAsync(s => s.Id == id && s.Project!.Organization.TenantId == tenant.CurrentTenant!.Id);

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
            .Include(s => s.Issue)
            .Include(s => s.Project).ThenInclude(p => p!.Organization)
            .FirstOrDefaultAsync(s => s.Id == id && s.Project!.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (session is null) return NotFound();

        if (session.Status is not (AgentSessionStatus.Failed or AgentSessionStatus.Cancelled))
            return Conflict(new { error = "Only failed or cancelled sessions can be retried.", session.Status, StatusName = session.Status.ToString() });

        var retryAgentId = body?.AgentIdOverride ?? session.AgentId;

        // Create a queued session record immediately so the UI can reflect the pending run right away,
        // before the ExecutionClient has a chance to pick up the Kafka message.
        var queuedSession = new AgentSession
        {
            Id = Guid.NewGuid(),
            AgentId = retryAgentId,
            IssueId = session.IssueId,
            ProjectId = session.ProjectId,
            Status = AgentSessionStatus.Pending,
        };
        db.AgentSessions.Add(queuedSession);
        await db.SaveChangesAsync();

        // Re-publish issue-assigned so the ExecutionClient starts the actual agent run.
        // AgentIdOverride allows using a different agent for the retry.
        object payload;
        if (session.IssueId.HasValue)
        {
            payload = new
            {
                id = session.IssueId,
                projectId = session.ProjectId,
                title = session.Issue?.Title ?? string.Empty,
                agentId = retryAgentId,
                sessionId = queuedSession.Id,
                dockerImageOverride = body?.DockerImageOverride,
                keepContainer = body?.KeepContainer ?? false,
                dockerCmdOverride = body?.DockerCmdOverride,
                modelOverride = body?.ModelOverride,
                runnerTypeOverride = body?.RunnerTypeOverride != null ? (int?)body.RunnerTypeOverride.Value : null,
                useHttpServerOverride = body?.UseHttpServerOverride,
                runtimeTypeOverride = body?.RuntimeTypeOverride != null ? (int?)body.RuntimeTypeOverride.Value : null,
                maxCiCdLoopCountOverride = body?.MaxCiCdLoopCountOverride,
                forceAgentId = body?.AgentIdOverride.HasValue ?? false,
            };
        }
        else
        {
            // Manual direct-start session retry
            payload = new
            {
                id = Guid.Empty,
                projectId = session.ProjectId,
                title = string.Empty,
                agentId = retryAgentId,
                sessionId = queuedSession.Id,
                isManualDirectStart = true,
                dockerImageOverride = body?.DockerImageOverride,
                keepContainer = body?.KeepContainer ?? false,
                dockerCmdOverride = body?.DockerCmdOverride,
                modelOverride = body?.ModelOverride,
                runnerTypeOverride = body?.RunnerTypeOverride != null ? (int?)body.RunnerTypeOverride.Value : null,
                useHttpServerOverride = body?.UseHttpServerOverride,
                runtimeTypeOverride = body?.RuntimeTypeOverride != null ? (int?)body.RuntimeTypeOverride.Value : null,
                maxCiCdLoopCountOverride = body?.MaxCiCdLoopCountOverride,
                forceAgentId = true,
                branch = session.GitBranch,
            };
        }

        try
        {
            await producer.ProduceAsync("issue-assigned", new Message<string, string>
            {
                Key = session.ProjectId.ToString(),
                Value = System.Text.Json.JsonSerializer.Serialize(payload),
            });
        }
        catch (Exception ex)
        {
            // Kafka publish failed — mark the pre-created session as failed and store the error.
            queuedSession.Status = AgentSessionStatus.Failed;
            queuedSession.EndedAt = DateTime.UtcNow;
            queuedSession.Warnings = System.Text.Json.JsonSerializer.Serialize(new[] { $"Failed to queue agent run: {ex.Message}" });
            await db.SaveChangesAsync();
        }

        return Accepted(new RetrySessionResponse(queuedSession.Id));
    }

    /// <summary>
    /// Triggers a CI/CD run for the current feature branch of a manual-mode agent session.
    /// Called from within the container by the <c>issuepit-trigger-cicd</c> script, authenticated
    /// using the ephemeral MCP token injected as the <c>ISSUEPIT_MCP_TOKEN</c> environment variable.
    /// </summary>
    [HttpPost("{id:guid}/trigger-cicd")]
    public async Task<IActionResult> TriggerCiCd(Guid id, CancellationToken cancellationToken)
    {
        var session = await db.AgentSessions
            .Include(s => s.Agent)
            .Include(s => s.Project).ThenInclude(p => p!.Organization)
            .Where(s => s.Id == id && s.Project!.Organization.TenantId == tenant.CurrentTenant!.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null) return NotFound();

        if (!session.Agent.ManualMode)
            return BadRequest(new { error = "CI/CD trigger is only available for manual-mode sessions." });

        var branch = session.GitBranch;

        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest(new { error = "No feature branch associated with this session. Push your branch first." });

        var repo = await db.GitRepositories
            .Where(r => r.ProjectId == session.ProjectId)
            .FirstOrDefaultAsync(cancellationToken);

        var commitSha = repo is not null
            ? gitService.GetBranchTipSha(repo, branch) ?? branch
            : branch;

        var run = await runQueue.EnqueueAsync(
            projectId: session.ProjectId,
            commitSha: commitSha,
            branch: branch,
            workflow: null,
            eventName: "push",
            inputs: null,
            gitRepoUrl: repo?.RemoteUrl,
            agentSessionId: session.Id,
            cancellationToken: cancellationToken);

        return Accepted(new TriggerCiCdResponse(run.Id, run.Status.ToString(), branch, commitSha));
    }

    [HttpPost("start-manual")]
    public async Task<IActionResult> StartManualSession([FromBody] StartManualSessionRequest request, CancellationToken cancellationToken)
    {
        if (tenant.CurrentTenant is null) return Unauthorized();

        var agent = await db.Agents
            .FirstOrDefaultAsync(a => a.Id == request.AgentId && a.Organization.TenantId == tenant.CurrentTenant.Id, cancellationToken);

        if (agent is null) return NotFound(new { error = "Agent not found." });

        if (!agent.ManualMode)
            return BadRequest(new { error = "Only manual-mode agents can be started via this endpoint." });

        var project = await db.Projects
            .Include(p => p.Organization)
            .Where(p => p.Id == request.ProjectId && p.Organization.TenantId == tenant.CurrentTenant.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null) return NotFound(new { error = "Project not found." });

        // Pre-create the session record (no placeholder issue needed — IssueId will remain null).
        var session = new AgentSession
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            ProjectId = project.Id,
            IssueId = null,
            Status = AgentSessionStatus.Pending,
        };
        db.AgentSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        // Publish a manual-direct-start message; the ExecutionClient skips issue lookup.
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            id = Guid.Empty,
            projectId = project.Id,
            title = string.Empty,
            agentId = agent.Id,
            sessionId = session.Id,
            isManualDirectStart = true,
            branch = request.Branch,
        });

        try
        {
            await producer.ProduceAsync("issue-assigned", new Message<string, string>
            {
                Key = project.Id.ToString(),
                Value = payload,
            });
        }
        catch (Exception ex)
        {
            session.Status = AgentSessionStatus.Failed;
            session.EndedAt = DateTime.UtcNow;
            session.Warnings = System.Text.Json.JsonSerializer.Serialize(new[] { $"Failed to queue agent run: {ex.Message}" });
            await db.SaveChangesAsync(cancellationToken);
        }

        return Accepted(new StartManualSessionResponse(session.Id));
    }

    /// <summary>Returns the ordered list of messages queued for an agent session.</summary>
    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid id)
    {
        var sessionExists = await db.AgentSessions
            .AnyAsync(s => s.Id == id && s.Project!.Organization.TenantId == tenant.CurrentTenant!.Id);

        if (!sessionExists) return NotFound();

        var messages = await db.AgentSessionMessages
            .Where(m => m.AgentSessionId == id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new AgentSessionMessageDto(
                m.Id,
                m.Content,
                m.Status.ToString(),
                m.ModelOverride,
                m.AgentIdOverride,
                m.AgentOverride != null ? m.AgentOverride.Name : null,
                m.CreatedAt,
                m.ProcessedAt))
            .ToListAsync();

        return Ok(messages);
    }

    /// <summary>Queues a new message to be processed by the agent in this session.</summary>
    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> QueueMessage(Guid id, [FromBody] QueueMessageRequest request, CancellationToken cancellationToken)
    {
        if (tenant.CurrentTenant is null) return Unauthorized();

        var session = await db.AgentSessions
            .Include(s => s.Project).ThenInclude(p => p!.Organization)
            .FirstOrDefaultAsync(s => s.Id == id && s.Project!.Organization.TenantId == tenant.CurrentTenant.Id, cancellationToken);

        if (session is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "Message content cannot be empty." });

        // Validate agent override if provided
        if (request.AgentIdOverride.HasValue)
        {
            var agentExists = await db.Agents
                .AnyAsync(a => a.Id == request.AgentIdOverride.Value && a.Organization.TenantId == tenant.CurrentTenant.Id, cancellationToken);
            if (!agentExists)
                return BadRequest(new { error = "Agent override not found." });
        }

        var message = new AgentSessionMessage
        {
            Id = Guid.NewGuid(),
            AgentSessionId = id,
            Content = request.Content.Trim(),
            ModelOverride = string.IsNullOrWhiteSpace(request.ModelOverride) ? null : request.ModelOverride.Trim(),
            AgentIdOverride = request.AgentIdOverride,
            CreatedAt = DateTime.UtcNow,
        };

        db.AgentSessionMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        return Created($"/api/agent-sessions/{id}/messages/{message.Id}",
            new AgentSessionMessageDto(message.Id, message.Content, message.Status.ToString(),
                message.ModelOverride, message.AgentIdOverride, null, message.CreatedAt, message.ProcessedAt));
    }

    /// <summary>Updates the content of a pending message. Returns 409 Conflict if the message is no longer pending.</summary>
    [HttpPatch("{id:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> UpdateMessage(Guid id, Guid messageId, [FromBody] UpdateMessageRequest request, CancellationToken cancellationToken)
    {
        if (tenant.CurrentTenant is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new UpdateMessageErrorResponse("Message content cannot be empty.", string.Empty));

        var message = await db.AgentSessionMessages
            .Include(m => m.AgentSession).ThenInclude(s => s!.Project).ThenInclude(p => p!.Organization)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.AgentSessionId == id
                && m.AgentSession!.Project!.Organization.TenantId == tenant.CurrentTenant.Id, cancellationToken);

        if (message is null) return NotFound();

        if (message.Status != AgentSessionMessageStatus.Pending)
            return Conflict(new UpdateMessageErrorResponse("Only pending messages can be edited.", message.Status.ToString()));

        message.Content = request.Content.Trim();
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new AgentSessionMessageDto(message.Id, message.Content, message.Status.ToString(),
            message.ModelOverride, message.AgentIdOverride, null, message.CreatedAt, message.ProcessedAt));
    }

    /// <summary>Cancels a pending message (cannot cancel running or done messages).</summary>
    [HttpDelete("{id:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> CancelMessage(Guid id, Guid messageId, CancellationToken cancellationToken)
    {
        if (tenant.CurrentTenant is null) return Unauthorized();

        var message = await db.AgentSessionMessages
            .Include(m => m.AgentSession).ThenInclude(s => s!.Project).ThenInclude(p => p!.Organization)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.AgentSessionId == id
                && m.AgentSession!.Project!.Organization.TenantId == tenant.CurrentTenant.Id, cancellationToken);

        if (message is null) return NotFound();

        if (message.Status != AgentSessionMessageStatus.Pending)
            return Conflict(new { error = "Only pending messages can be cancelled.", message.Status, StatusName = message.Status.ToString() });

        message.Status = AgentSessionMessageStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new { message.Id, Status = message.Status.ToString() });
    }
}

public record RetrySessionResponse(Guid RetriedSessionId);

public record TriggerCiCdResponse(Guid RunId, string Status, string Branch, string CommitSha);

public record RetrySessionRequest(
    string? DockerImageOverride = null,
    bool KeepContainer = false,
    string[]? DockerCmdOverride = null,
    /// <summary>Override the agent used for this retry run. Null = use the same agent as the original session.</summary>
    Guid? AgentIdOverride = null,
    /// <summary>Override the model used for this retry run. Null = use the agent's configured model.</summary>
    string? ModelOverride = null,
    /// <summary>Override the runner (CLI) type for this retry. Null = use the agent's configured RunnerType.</summary>
    RunnerType? RunnerTypeOverride = null,
    /// <summary>Override whether to use HTTP server mode (opencode only). Null = use the agent's setting.</summary>
    bool? UseHttpServerOverride = null,
    /// <summary>Override the runtime type (Docker, Native, SSH…) for this retry. Null = use the org default.</summary>
    RuntimeType? RuntimeTypeOverride = null,
    /// <summary>Override the maximum number of CI/CD → agent-fix loop iterations for this retry. Null = use the project/org/system default.</summary>
    int? MaxCiCdLoopCountOverride = null);

public record StartManualSessionRequest(
    Guid AgentId,
    Guid ProjectId,
    string? Branch = null,
    string? Description = null);

public record StartManualSessionResponse(Guid SessionId);

/// <summary>A single workflow phase summary derived from agent session logs, returned by GET /api/agent-sessions/{id}/steps.</summary>
public record AgentSessionStepDto(
    string Section,
    int SectionIndex,
    string Label,
    bool HasError,
    DateTime StartedAt,
    DateTime EndedAt);

public record QueueMessageRequest(
    string Content,
    string? ModelOverride = null,
    Guid? AgentIdOverride = null);

public record UpdateMessageRequest(string Content);

public record UpdateMessageErrorResponse(string Error, string Status);

public record AgentSessionMessageDto(
    Guid Id,
    string Content,
    string Status,
    string? ModelOverride,
    Guid? AgentIdOverride,
    string? AgentOverrideName,
    DateTime CreatedAt,
    DateTime? ProcessedAt);
