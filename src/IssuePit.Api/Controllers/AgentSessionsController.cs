using IssuePit.Api.Services;
using IssuePit.Core.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/agent-sessions")]
public class AgentSessionsController(IssuePitDbContext db, TenantContext tenant) : ControllerBase
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
}
