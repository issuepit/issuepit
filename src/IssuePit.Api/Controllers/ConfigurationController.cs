using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigurationController(IssuePitDbContext db, TenantContext tenant) : ControllerBase
{
    // --- API Keys ---

    [HttpGet("keys")]
    public async Task<IActionResult> GetKeys()
    {
        var keys = await db.ApiKeys
            .Where(k => k.Organization.TenantId == tenant.CurrentTenant!.Id)
            .Select(k => new
            {
                k.Id,
                k.OrgId,
                k.Name,
                k.Provider,
                ProviderName = k.Provider.ToString(),
                k.CreatedAt,
                k.ExpiresAt,
                // Never return the encrypted value
            })
            .ToListAsync();
        return Ok(keys);
    }

    [HttpPost("keys")]
    public async Task<IActionResult> CreateKey([FromBody] ApiKeyRequest req)
    {
        var key = new ApiKey
        {
            Id = Guid.NewGuid(),
            OrgId = req.OrgId,
            Name = req.Name,
            Provider = req.Provider,
            // In production, encrypt before storing. Placeholder prefix marks it as unencrypted for now.
            EncryptedValue = $"plain:{req.Value}",
            ExpiresAt = req.ExpiresAt,
        };
        db.ApiKeys.Add(key);
        await db.SaveChangesAsync();
        return Created($"/api/config/keys/{key.Id}", new { key.Id, key.Name, key.Provider, key.CreatedAt });
    }

    [HttpDelete("keys/{id:guid}")]
    public async Task<IActionResult> DeleteKey(Guid id)
    {
        var key = await db.ApiKeys.FindAsync(id);
        if (key is null) return NotFound();
        db.ApiKeys.Remove(key);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Runtime Configurations ---

    [HttpGet("runtimes")]
    public async Task<IActionResult> GetRuntimes()
    {
        var runtimes = await db.RuntimeConfigurations
            .Where(r => r.Organization.TenantId == tenant.CurrentTenant!.Id)
            .Select(r => new
            {
                r.Id,
                r.OrgId,
                r.Name,
                r.Type,
                TypeName = r.Type.ToString(),
                r.Configuration,
                r.IsDefault,
                r.CreatedAt,
            })
            .ToListAsync();
        return Ok(runtimes);
    }

    [HttpPost("runtimes")]
    public async Task<IActionResult> CreateRuntime([FromBody] RuntimeConfigRequest req)
    {
        if (req.IsDefault)
        {
            // Clear existing default for this org
            var existing = await db.RuntimeConfigurations
                .Where(r => r.OrgId == req.OrgId && r.IsDefault)
                .ToListAsync();
            existing.ForEach(r => r.IsDefault = false);
        }

        var runtime = new RuntimeConfiguration
        {
            Id = Guid.NewGuid(),
            OrgId = req.OrgId,
            Name = req.Name,
            Type = req.Type,
            Configuration = req.Configuration,
            IsDefault = req.IsDefault,
        };
        db.RuntimeConfigurations.Add(runtime);
        await db.SaveChangesAsync();
        return Created($"/api/config/runtimes/{runtime.Id}", runtime);
    }

    [HttpPut("runtimes/{id:guid}")]
    public async Task<IActionResult> UpdateRuntime(Guid id, [FromBody] RuntimeConfigRequest req)
    {
        var runtime = await db.RuntimeConfigurations.FindAsync(id);
        if (runtime is null) return NotFound();

        if (req.IsDefault && !runtime.IsDefault)
        {
            var existing = await db.RuntimeConfigurations
                .Where(r => r.OrgId == runtime.OrgId && r.IsDefault && r.Id != id)
                .ToListAsync();
            existing.ForEach(r => r.IsDefault = false);
        }

        runtime.Name = req.Name;
        runtime.Type = req.Type;
        runtime.Configuration = req.Configuration;
        runtime.IsDefault = req.IsDefault;
        await db.SaveChangesAsync();
        return Ok(runtime);
    }

    [HttpDelete("runtimes/{id:guid}")]
    public async Task<IActionResult> DeleteRuntime(Guid id)
    {
        var runtime = await db.RuntimeConfigurations.FindAsync(id);
        if (runtime is null) return NotFound();
        db.RuntimeConfigurations.Remove(runtime);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record ApiKeyRequest(Guid OrgId, string Name, ApiKeyProvider Provider, string Value, DateTime? ExpiresAt);
public record RuntimeConfigRequest(Guid OrgId, string Name, RuntimeType Type, string Configuration, bool IsDefault);
