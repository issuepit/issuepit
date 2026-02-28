using Confluent.Kafka;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IssuePit.Api.Endpoints;

public static class IssueEndpoints
{
    public static void MapIssueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/issues");

        group.MapGet("/", async (Guid projectId, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var issues = await db.Issues
                .Include(i => i.Labels)
                .Where(i => i.ProjectId == projectId)
                .ToListAsync();
            return Results.Ok(issues);
        });

        group.MapGet("/{id:guid}", async (Guid id, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var issue = await db.Issues
                .Include(i => i.Labels)
                .Include(i => i.SubIssues)
                .Include(i => i.Assignees)
                .FirstOrDefaultAsync(i => i.Id == id);
            return issue is null ? Results.NotFound() : Results.Ok(issue);
        });

        group.MapGet("/{id:guid}/sub-issues", async (Guid id, IssuePitDbContext db) =>
        {
            var subIssues = await db.Issues
                .Where(i => i.ParentIssueId == id)
                .ToListAsync();
            return Results.Ok(subIssues);
        });

        group.MapPost("/", async (Issue issue, IssuePitDbContext db, TenantContext ctx, IProducer<string, string> producer) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            issue.Id = Guid.NewGuid();
            issue.CreatedAt = DateTime.UtcNow;
            issue.UpdatedAt = DateTime.UtcNow;

            var maxNumber = await db.Issues
                .Where(i => i.ProjectId == issue.ProjectId)
                .MaxAsync(i => (int?)i.Number) ?? 0;
            issue.Number = maxNumber + 1;

            db.Issues.Add(issue);
            await db.SaveChangesAsync();

            await producer.ProduceAsync("issue-assigned", new Message<string, string>
            {
                Key = issue.Id.ToString(),
                Value = JsonSerializer.Serialize(new { issue.Id, issue.ProjectId, issue.Title })
            });

            return Results.Created($"/api/issues/{issue.Id}", issue);
        });

        group.MapPut("/{id:guid}", async (Guid id, Issue updated, IssuePitDbContext db) =>
        {
            var issue = await db.Issues.FindAsync(id);
            if (issue is null) return Results.NotFound();
            issue.Title = updated.Title;
            issue.Body = updated.Body;
            issue.Status = updated.Status;
            issue.Priority = updated.Priority;
            issue.Type = updated.Type;
            issue.GitBranch = updated.GitBranch;
            issue.MilestoneId = updated.MilestoneId;
            issue.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(issue);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IssuePitDbContext db) =>
        {
            var issue = await db.Issues.FindAsync(id);
            if (issue is null) return Results.NotFound();
            db.Issues.Remove(issue);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
