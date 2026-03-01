using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/admin/tenants")]
public class TenantsController(IssuePitDbContext db, TenantDatabaseService dbService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTenants()
    {
        var tenants = await db.Tenants.OrderBy(t => t.Name).ToListAsync();
        return Ok(tenants);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTenant(Guid id)
    {
        var tenant = await db.Tenants.FindAsync(id);
        return tenant is null ? NotFound() : Ok(tenant);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTenant([FromBody] TenantRequest req)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Hostname = req.Hostname,
            CreatedAt = DateTime.UtcNow
        };

        if (req.ProvisionDatabase)
        {
            tenant.DatabaseConnectionString = await dbService.ProvisionDatabaseAsync(req.Name);
        }

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return Created($"/api/admin/tenants/{tenant.Id}", tenant);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTenant(Guid id, [FromBody] TenantRequest req)
    {
        var tenant = await db.Tenants.FindAsync(id);
        if (tenant is null) return NotFound();
        tenant.Name = req.Name;
        tenant.Hostname = req.Hostname;
        await db.SaveChangesAsync();
        return Ok(tenant);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTenant(Guid id)
    {
        var tenant = await db.Tenants.FindAsync(id);
        if (tenant is null) return NotFound();
        db.Tenants.Remove(tenant);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record TenantRequest(string Name, string Hostname, bool ProvisionDatabase = false);
