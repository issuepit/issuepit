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
            .Include(s => s.Issue).ThenInclude(i => i.Project)
            .Include(s => s.CiCdRuns)
            .Where(s => s.Id == id && s.Issue.Project!.Organization.TenantId == tenant.CurrentTenant!.Id)
            .Select(s => new
            {
                s.Id,
                s.AgentId,
                AgentName = s.Agent.Name,
                IsManualMode = s.Agent.ManualMode,
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
                Section = l.Section != null ? l.Section.ToString() : null,
                l.SectionIndex,
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

        var retryAgentId = body?.AgentIdOverride ?? session.AgentId;

        // Create a queued session record immediately so the UI can reflect the pending run right away,
        // before the ExecutionClient has a chance to pick up the Kafka message.
        var queuedSession = new AgentSession
        {
            Id = Guid.NewGuid(),
            AgentId = retryAgentId,
            IssueId = session.IssueId,
            Status = AgentSessionStatus.Pending,
        };
        db.AgentSessions.Add(queuedSession);
        await db.SaveChangesAsync();

        // Re-publish issue-assigned so the ExecutionClient starts the actual agent run.
        // AgentIdOverride allows using a different agent for the retry.
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            id = session.IssueId,
            projectId = session.Issue.ProjectId,
            title = session.Issue.Title,
            agentId = retryAgentId,
            sessionId = queuedSession.Id,
            dockerImageOverride = body?.DockerImageOverride,
            keepContainer = body?.KeepContainer ?? false,
            dockerCmdOverride = body?.DockerCmdOverride,
            modelOverride = body?.ModelOverride,
            runnerTypeOverride = body?.RunnerTypeOverride != null ? (int?)body.RunnerTypeOverride.Value : null,
            useHttpServerOverride = body?.UseHttpServerOverride,
            runtimeTypeOverride = body?.RuntimeTypeOverride != null ? (int?)body.RuntimeTypeOverride.Value : null,
            // When retrying with a different agent, bypass the assignee check so the run is queued.
            forceAgentId = body?.AgentIdOverride.HasValue ?? false,
        });

        try
        {
            await producer.ProduceAsync("issue-assigned", new Message<string, string>
            {
                Key = session.IssueId.ToString(),
                Value = payload,
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
            .Include(s => s.Issue).ThenInclude(i => i!.Project).ThenInclude(p => p!.Organization)
            .Where(s => s.Id == id && s.Issue!.Project!.Organization.TenantId == tenant.CurrentTenant!.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null) return NotFound();

        if (session.Issue?.ProjectId is null)
            return BadRequest(new { error = "Session is not linked to a project." });

        if (!session.Agent.ManualMode)
            return BadRequest(new { error = "CI/CD trigger is only available for manual-mode sessions." });

        var issue = session.Issue;
        var branch = issue.GitBranch;

        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest(new { error = "No feature branch associated with this session. Push your branch first." });

        var repo = await db.GitRepositories
            .Where(r => r.ProjectId == issue.ProjectId)
            .FirstOrDefaultAsync(cancellationToken);

        var commitSha = repo is not null
            ? gitService.GetBranchTipSha(repo, branch) ?? branch
            : branch;

        var run = await runQueue.EnqueueAsync(
            projectId: issue.ProjectId,
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

    /// <summary>
    /// Starts a manual-mode agent session without requiring an existing issue.
    /// Creates a lightweight placeholder issue tied to the project so the session can be tracked,
    /// then queues the agent run via the normal Kafka pipeline.
    /// </summary>
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

        // Create a lightweight placeholder issue so the session has something to anchor to.
        var maxNumber = await db.Issues
            .Where(i => i.ProjectId == project.Id)
            .MaxAsync(i => (int?)i.Number, cancellationToken) ?? 0;

        var title = string.IsNullOrWhiteSpace(request.Description)
            ? $"Manual session {DateTime.UtcNow:yyyy-MM-dd HH:mm}"
            : request.Description;

        var placeholderIssue = new Issue
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = title,
            Body = $"Placeholder issue created automatically for a manual agent session.\nBranch: {request.Branch ?? "(default)"}",
            Status = IssueStatus.InProgress,
            Type = IssueType.Task,
            Number = maxNumber + 1,
            GitBranch = request.Branch,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
        };
        db.Issues.Add(placeholderIssue);

        // Pre-create the session record so the UI can navigate to it immediately.
        var session = new AgentSession
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            IssueId = placeholderIssue.Id,
            Status = AgentSessionStatus.Pending,
        };
        db.AgentSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        // Publish issue-assigned event; ForceAgentId bypasses the assignee check.
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            id = placeholderIssue.Id,
            projectId = project.Id,
            title = placeholderIssue.Title,
            agentId = agent.Id,
            sessionId = session.Id,
            forceAgentId = true,
            branch = request.Branch,
        });

        try
        {
            await producer.ProduceAsync("issue-assigned", new Message<string, string>
            {
                Key = placeholderIssue.Id.ToString(),
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

        return Accepted(new StartManualSessionResponse(session.Id, placeholderIssue.Id));
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
    RuntimeType? RuntimeTypeOverride = null);

public record StartManualSessionRequest(
    Guid AgentId,
    Guid ProjectId,
    string? Branch = null,
    string? Description = null);

public record StartManualSessionResponse(Guid SessionId, Guid PlaceholderIssueId);
