using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/agents")]
public class AgentsController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAgents()
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var agents = await db.Agents
            .Include(a => a.Organization)
            .Where(a => a.Organization.TenantId == ctx.CurrentTenant.Id)
            .ToListAsync();
        return Ok(agents);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAgent(Guid id)
    {
        var agent = await db.Agents
            .Include(a => a.AgentMcpServers)
            .ThenInclude(am => am.McpServer)
            .Include(a => a.ChildAgents)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (agent is null) return NotFound();
        return Ok(new AgentDetailResponse(
            agent.Id,
            agent.OrgId,
            agent.Name,
            agent.SystemPrompt,
            agent.DockerImage,
            agent.AllowedTools,
            agent.RunnerType,
            agent.Model,
            agent.IsActive,
            agent.ParentAgentId,
            agent.AgentType,
            agent.UseHttpServer,
            !string.IsNullOrEmpty(agent.HttpServerPassword),
            agent.ManualMode,
            agent.IsShellAgent,
            agent.OpenCodeAgentName,
            agent.AutoSummarize,
            agent.CreatedAt,
            agent.AgentMcpServers.Select(am => new LinkedMcpServerDto(
                am.McpServer.Id,
                am.McpServer.Name,
                am.McpServer.Url,
                am.McpServer.Description,
                am.McpServer.AllowedTools)).ToList(),
            agent.ChildAgents.Select(c => new ChildAgentDto(
                c.Id, c.Name, c.Model, c.SystemPrompt, c.AgentType, c.IsActive)).ToList(),
            agent.ConfigFieldSources));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAgent([FromBody] CreateAgentRequest request)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!Guid.TryParse(request.OrgId, out var orgId) || orgId == Guid.Empty)
            return BadRequest(new { error = "A valid non-empty organization ID (GUID) is required." });
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = request.Name,
            SystemPrompt = request.SystemPrompt ?? string.Empty,
            DockerImage = string.IsNullOrEmpty(request.DockerImage) ? null : request.DockerImage,
            AllowedTools = request.AllowedTools ?? "[]",
            RunnerType = request.RunnerType,
            Model = string.IsNullOrEmpty(request.Model) ? null : request.Model,
            IsActive = request.IsActive,
            ParentAgentId = request.ParentAgentId,
            AgentType = request.AgentType,
            UseHttpServer = request.UseHttpServer,
            HttpServerPassword = string.IsNullOrEmpty(request.HttpServerPassword) ? null : request.HttpServerPassword,
            ManualMode = request.ManualMode,
            DisableInternet = request.DisableInternet,
            AutoSummarize = request.AutoSummarize,
            CreatedAt = DateTime.UtcNow,
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();
        // Return the created agent without the password (security: never expose credentials).
        return Created($"/api/agents/{agent.Id}", ToAgentResponse(agent));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAgent(Guid id, [FromBody] Agent updated)
    {
        var agent = await db.Agents.FindAsync(id);
        if (agent is null) return NotFound();
        agent.Name = updated.Name;
        agent.SystemPrompt = updated.SystemPrompt;
        agent.DockerImage = updated.DockerImage;
        agent.AllowedTools = updated.AllowedTools;
        agent.RunnerType = updated.RunnerType;
        agent.Model = updated.Model;
        agent.IsActive = updated.IsActive;
        agent.ParentAgentId = updated.ParentAgentId;
        agent.AgentType = updated.AgentType;
        agent.UseHttpServer = updated.UseHttpServer;
        agent.ManualMode = updated.ManualMode;
        agent.IsShellAgent = updated.IsShellAgent;
        agent.OpenCodeAgentName = updated.OpenCodeAgentName;
        agent.AutoSummarize = updated.AutoSummarize;
        // Only update password when a non-empty value is provided so a blank PUT does not clear it.
        // To clear the password, use a dedicated PATCH endpoint (not yet implemented) or
        // delete and recreate the agent. This prevents accidental password removal on a full update.
        if (!string.IsNullOrEmpty(updated.HttpServerPassword))
            agent.HttpServerPassword = updated.HttpServerPassword;
        await db.SaveChangesAsync();
        // Return the agent without the password (security: never expose credentials in responses).
        return Ok(ToAgentResponse(agent));
    }

    [HttpPatch("{id:guid}/active")]
    public async Task<IActionResult> SetAgentActive(Guid id, [FromBody] SetAgentActiveRequest request)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var agent = await db.Agents.Include(a => a.Organization).FirstOrDefaultAsync(a => a.Id == id);
        if (agent is null) return NotFound();
        if (agent.Organization.TenantId != ctx.CurrentTenant.Id) return Forbid();
        agent.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return Ok(agent);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAgent(Guid id)
    {
        var agent = await db.Agents.FindAsync(id);
        if (agent is null) return NotFound();
        db.Agents.Remove(agent);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{agentId:guid}/mcp-servers/{mcpServerId:guid}")]
    public async Task<IActionResult> LinkMcpServer(Guid agentId, Guid mcpServerId)
    {
        var link = new AgentMcpServer { AgentId = agentId, McpServerId = mcpServerId };
        db.AgentMcpServers.Add(link);
        await db.SaveChangesAsync();
        return Created($"/api/agents/{agentId}/mcp-servers/{mcpServerId}", link);
    }

    [HttpDelete("{agentId:guid}/mcp-servers/{mcpServerId:guid}")]
    public async Task<IActionResult> UnlinkMcpServer(Guid agentId, Guid mcpServerId)
    {
        var link = await db.AgentMcpServers.FindAsync(agentId, mcpServerId);
        if (link is null) return NotFound();
        db.AgentMcpServers.Remove(link);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static AgentResponse ToAgentResponse(Agent agent) => new(
        agent.Id,
        agent.OrgId,
        agent.Name,
        agent.SystemPrompt,
        agent.DockerImage,
        agent.AllowedTools,
        agent.RunnerType,
        agent.Model,
        agent.IsActive,
        agent.ParentAgentId,
        agent.AgentType,
        agent.UseHttpServer,
        agent.ManualMode,
        agent.IsShellAgent,
        agent.OpenCodeAgentName,
        agent.AutoSummarize,
        agent.CreatedAt,
        agent.ConfigFieldSources);
}

public sealed record SetAgentActiveRequest(bool IsActive);

/// <summary>Request body for POST /api/agents. Uses string OrgId to handle empty-string input gracefully.</summary>
public sealed record CreateAgentRequest(
    string? OrgId,
    string Name,
    string? SystemPrompt,
    string? DockerImage,
    string? AllowedTools,
    RunnerType? RunnerType,
    string? Model,
    bool IsActive,
    Guid? ParentAgentId,
    OpenCodeAgentType? AgentType,
    bool UseHttpServer,
    string? HttpServerPassword,
    bool ManualMode,
    bool DisableInternet,
    bool AutoSummarize);

/// <summary>Agent summary returned by POST (create) and PUT (update) endpoints.</summary>
public sealed record AgentResponse(
    Guid Id,
    Guid OrgId,
    string Name,
    string SystemPrompt,
    string? DockerImage,
    string AllowedTools,
    RunnerType? RunnerType,
    string? Model,
    bool IsActive,
    Guid? ParentAgentId,
    OpenCodeAgentType? AgentType,
    bool UseHttpServer,
    bool ManualMode,
    bool IsShellAgent,
    string? OpenCodeAgentName,
    bool AutoSummarize,
    DateTime CreatedAt,
    Dictionary<string, string>? ConfigFieldSources);

/// <summary>Detailed agent response returned by GET /agents/{id}, including linked MCP servers and child agents.</summary>
public sealed record AgentDetailResponse(
    Guid Id,
    Guid OrgId,
    string Name,
    string SystemPrompt,
    string? DockerImage,
    string AllowedTools,
    RunnerType? RunnerType,
    string? Model,
    bool IsActive,
    Guid? ParentAgentId,
    OpenCodeAgentType? AgentType,
    bool UseHttpServer,
    bool HasHttpServerPassword,
    bool ManualMode,
    bool IsShellAgent,
    string? OpenCodeAgentName,
    bool AutoSummarize,
    DateTime CreatedAt,
    IReadOnlyList<LinkedMcpServerDto> LinkedMcpServers,
    IReadOnlyList<ChildAgentDto> ChildAgents,
    Dictionary<string, string>? ConfigFieldSources);

/// <summary>MCP server link summary included in agent detail responses.</summary>
public sealed record LinkedMcpServerDto(
    Guid Id,
    string Name,
    string Url,
    string? Description,
    string AllowedTools);

/// <summary>Child agent summary included in agent detail responses.</summary>
public sealed record ChildAgentDto(
    Guid Id,
    string Name,
    string? Model,
    string SystemPrompt,
    OpenCodeAgentType? AgentType,
    bool IsActive);
