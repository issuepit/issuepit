using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/mcp-servers")]
public class McpServersController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMcpServers()
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var servers = await db.McpServers
            .Where(m => db.Organizations.Any(o => o.Id == m.OrgId && o.TenantId == ctx.CurrentTenant.Id))
            .ToListAsync();
        return Ok(servers);
    }

    [HttpPost]
    public async Task<IActionResult> CreateMcpServer([FromBody] McpServer server)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        server.Id = Guid.NewGuid();
        server.CreatedAt = DateTime.UtcNow;
        db.McpServers.Add(server);
        await db.SaveChangesAsync();
        return Created($"/api/mcp-servers/{server.Id}", server);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMcpServer(Guid id)
    {
        var server = await db.McpServers.FindAsync(id);
        if (server is null) return NotFound();
        db.McpServers.Remove(server);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
