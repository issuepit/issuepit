using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
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
            .FirstOrDefaultAsync(a => a.Id == id);
        return agent is null ? NotFound() : Ok(agent);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAgent([FromBody] Agent agent)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        agent.Id = Guid.NewGuid();
        agent.CreatedAt = DateTime.UtcNow;
        db.Agents.Add(agent);
        await db.SaveChangesAsync();
        return Created($"/api/agents/{agent.Id}", agent);
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
}
