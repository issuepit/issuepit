using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Endpoints;

public static class CiCdEndpoints
{
    public static IEndpointRouteBuilder MapCiCdEndpoints(this IEndpointRouteBuilder app)
    {
        var cicd = app.MapGroup("/api/cicd-runs");

        cicd.MapGet("/", async (Guid? projectId, IssuePitDbContext db, TenantContext tenant) =>
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
                })
                .Take(100)
                .ToListAsync();

            return Results.Ok(runs);
        });

        cicd.MapGet("/{id:guid}", async (Guid id, IssuePitDbContext db, TenantContext tenant) =>
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
                })
                .FirstOrDefaultAsync();

            return run is null ? Results.NotFound() : Results.Ok(run);
        });

        cicd.MapGet("/{id:guid}/logs", async (Guid id, LogStream? stream, IssuePitDbContext db, TenantContext tenant) =>
        {
            // Verify the run belongs to this tenant
            var runExists = await db.CiCdRuns
                .AnyAsync(r => r.Id == id && r.Project.Organization.TenantId == tenant.CurrentTenant!.Id);

            if (!runExists) return Results.NotFound();

            var query = db.CiCdRunLogs
                .Where(l => l.CiCdRunId == id)
                .OrderBy(l => l.Timestamp)
                .AsQueryable();

            if (stream.HasValue)
                query = query.Where(l => l.Stream == stream.Value);

            var logs = await query
                .Select(l => new { l.Id, l.Line, l.Stream, StreamName = l.Stream.ToString(), l.Timestamp })
                .ToListAsync();

            return Results.Ok(logs);
        });

        return app;
    }
}
