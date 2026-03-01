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

        mcpGroup.MapGet("/templates", () =>
        {
            return Results.Ok(McpServerTemplates.All);
        });

        mcpGroup.MapGet("/{id:guid}", async (Guid id, IssuePitDbContext db) =>
        {
            var server = await db.McpServers
                .Include(m => m.AgentMcpServers)
                .ThenInclude(am => am.Agent)
                .FirstOrDefaultAsync(m => m.Id == id);
            return server is null ? Results.NotFound() : Results.Ok(server);
        });

        mcpGroup.MapPost("/", async (McpServerRequest req, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var server = new McpServer
            {
                Id = Guid.NewGuid(),
                OrgId = req.OrgId,
                Name = req.Name,
                Description = req.Description,
                Url = req.Url,
                AllowedTools = req.AllowedTools,
                Configuration = req.Configuration,
                CreatedAt = DateTime.UtcNow,
            };
            db.McpServers.Add(server);
            await db.SaveChangesAsync();
            return Results.Created($"/api/mcp-servers/{server.Id}", server);
        });

        mcpGroup.MapPut("/{id:guid}", async (Guid id, McpServerRequest req, IssuePitDbContext db) =>
        {
            var server = await db.McpServers.FindAsync(id);
            if (server is null) return Results.NotFound();
            server.Name = req.Name;
            server.Description = req.Description;
            server.Url = req.Url;
            server.AllowedTools = req.AllowedTools;
            server.Configuration = req.Configuration;
            await db.SaveChangesAsync();
            return Results.Ok(server);
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
            var exists = await db.AgentMcpServers.FindAsync(agentId, mcpServerId);
            if (exists is not null) return Results.Conflict();
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

file record McpServerRequest(
    Guid OrgId,
    string Name,
    string? Description,
    string Url,
    string AllowedTools,
    string Configuration
);

file static class McpServerTemplates
{
    public static readonly IReadOnlyList<object> All =
    [
        new
        {
            Key = "github",
            Name = "GitHub MCP",
            Description = "Manage GitHub repositories, issues, pull requests, and more.",
            Url = "npx @modelcontextprotocol/server-github",
            AllowedTools = """["search_repositories","get_file_contents","create_issue","list_issues","get_issue","create_pull_request","list_pull_requests","get_pull_request"]""",
            Configuration = """{"env":{"GITHUB_PERSONAL_ACCESS_TOKEN":""}}""",
        },
        new
        {
            Key = "playwright",
            Name = "Playwright MCP",
            Description = "Automate browsers for web scraping and UI testing.",
            Url = "npx @modelcontextprotocol/server-playwright",
            AllowedTools = """["navigate","screenshot","click","fill","evaluate","wait_for_selector"]""",
            Configuration = """{"headless":true,"timeout":30000}""",
        },
        new
        {
            Key = "filesystem",
            Name = "Filesystem MCP",
            Description = "Read and write files on the agent host filesystem.",
            Url = "npx @modelcontextprotocol/server-filesystem",
            AllowedTools = """["read_file","write_file","list_directory","create_directory","delete_file","move_file"]""",
            Configuration = """{"rootPath":"/workspace"}""",
        },
        new
        {
            Key = "web-search",
            Name = "Web Search MCP",
            Description = "Search the web using Brave Search API.",
            Url = "npx @modelcontextprotocol/server-brave-search",
            AllowedTools = """["search","search_news","search_images"]""",
            Configuration = """{"env":{"BRAVE_API_KEY":""}}""",
        },
        new
        {
            Key = "gitlab",
            Name = "GitLab MCP",
            Description = "Manage GitLab projects, issues, merge requests, and CI/CD pipelines.",
            Url = "npx @modelcontextprotocol/server-gitlab",
            AllowedTools = """["list_projects","get_project","create_issue","list_issues","create_merge_request","list_pipelines","trigger_pipeline"]""",
            Configuration = """{"env":{"GITLAB_TOKEN":"","GITLAB_URL":"https://gitlab.com"}}""",
        },
    ];
}
