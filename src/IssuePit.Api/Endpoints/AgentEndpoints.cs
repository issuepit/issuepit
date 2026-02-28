using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Endpoints;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/agents");

        group.MapGet("/", async (IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var agents = await db.Agents
                .Include(a => a.Organization)
                .Where(a => a.Organization.TenantId == ctx.CurrentTenant.Id)
                .ToListAsync();
            return Results.Ok(agents);
        });

        group.MapGet("/{id:guid}", async (Guid id, IssuePitDbContext db) =>
        {
            var agent = await db.Agents
                .Include(a => a.AgentMcpServers)
                .ThenInclude(am => am.McpServer)
                .FirstOrDefaultAsync(a => a.Id == id);
            return agent is null ? Results.NotFound() : Results.Ok(agent);
        });

        group.MapPost("/", async (Agent agent, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            agent.Id = Guid.NewGuid();
            agent.CreatedAt = DateTime.UtcNow;
            db.Agents.Add(agent);
            await db.SaveChangesAsync();
            return Results.Created($"/api/agents/{agent.Id}", agent);
        });

        group.MapPut("/{id:guid}", async (Guid id, Agent updated, IssuePitDbContext db) =>
        {
            var agent = await db.Agents.FindAsync(id);
            if (agent is null) return Results.NotFound();
            agent.Name = updated.Name;
            agent.SystemPrompt = updated.SystemPrompt;
            agent.DockerImage = updated.DockerImage;
            agent.AllowedTools = updated.AllowedTools;
            await db.SaveChangesAsync();
            return Results.Ok(agent);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IssuePitDbContext db) =>
        {
            var agent = await db.Agents.FindAsync(id);
            if (agent is null) return Results.NotFound();
            db.Agents.Remove(agent);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // MCP Servers
        var mcpGroup = app.MapGroup("/api/mcp-servers");

        mcpGroup.MapGet("/", async (IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var servers = await db.McpServers
                .Where(m => db.Organizations.Any(o => o.Id == m.OrgId && o.TenantId == ctx.CurrentTenant.Id))
                .ToListAsync();
            return Results.Ok(servers);
        });

        mcpGroup.MapPost("/", async (McpServer server, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            server.Id = Guid.NewGuid();
            server.CreatedAt = DateTime.UtcNow;
            db.McpServers.Add(server);
            await db.SaveChangesAsync();
            return Results.Created($"/api/mcp-servers/{server.Id}", server);
        });

        mcpGroup.MapDelete("/{id:guid}", async (Guid id, IssuePitDbContext db) =>
        {
            var server = await db.McpServers.FindAsync(id);
            if (server is null) return Results.NotFound();
            db.McpServers.Remove(server);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Link agent to MCP server
        group.MapPost("/{agentId:guid}/mcp-servers/{mcpServerId:guid}", async (Guid agentId, Guid mcpServerId, IssuePitDbContext db) =>
        {
            var link = new AgentMcpServer { AgentId = agentId, McpServerId = mcpServerId };
            db.AgentMcpServers.Add(link);
            await db.SaveChangesAsync();
            return Results.Created($"/api/agents/{agentId}/mcp-servers/{mcpServerId}", link);
        });

        group.MapDelete("/{agentId:guid}/mcp-servers/{mcpServerId:guid}", async (Guid agentId, Guid mcpServerId, IssuePitDbContext db) =>
        {
            var link = await db.AgentMcpServers.FindAsync(agentId, mcpServerId);
            if (link is null) return Results.NotFound();
            db.AgentMcpServers.Remove(link);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
