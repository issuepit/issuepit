using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/properties")]
public class ProjectPropertiesController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    // ── Project Properties ────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetProperties(Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var props = await db.ProjectProperties
            .Where(p => p.ProjectId == projectId)
            .OrderBy(p => p.Position)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync();
        return Ok(props);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProperty(Guid projectId, [FromBody] CreatePropertyRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var pos = await db.ProjectProperties.Where(p => p.ProjectId == projectId).CountAsync();
        var prop = new ProjectProperty
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = req.Name,
            Type = req.Type,
            IsRequired = req.IsRequired,
            DefaultValue = req.DefaultValue,
            AllowedValues = req.AllowedValues,
            Position = pos,
            CreatedAt = DateTime.UtcNow,
        };
        db.ProjectProperties.Add(prop);
        await db.SaveChangesAsync();
        return Created($"/api/projects/{projectId}/properties/{prop.Id}", prop);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateProperty(Guid projectId, Guid id, [FromBody] UpdatePropertyRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var prop = await db.ProjectProperties.FirstOrDefaultAsync(p => p.Id == id && p.ProjectId == projectId);
        if (prop is null) return NotFound();
        prop.Name = req.Name;
        prop.Type = req.Type;
        prop.IsRequired = req.IsRequired;
        prop.DefaultValue = req.DefaultValue;
        prop.AllowedValues = req.AllowedValues;
        await db.SaveChangesAsync();
        return Ok(prop);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProperty(Guid projectId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var prop = await db.ProjectProperties.FirstOrDefaultAsync(p => p.Id == id && p.ProjectId == projectId);
        if (prop is null) return NotFound();
        db.ProjectProperties.Remove(prop);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Issue Property Values ─────────────────────────────────────────────

    [HttpGet("/api/projects/{projectId:guid}/issues/{issueId:guid}/property-values")]
    public async Task<IActionResult> GetIssuePropertyValues(Guid projectId, Guid issueId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var values = await db.IssuePropertyValues
            .Include(v => v.Property)
            .Where(v => v.IssueId == issueId && v.Property.ProjectId == projectId)
            .ToListAsync();
        return Ok(values);
    }

    [HttpPut("/api/projects/{projectId:guid}/issues/{issueId:guid}/property-values/{propertyId:guid}")]
    public async Task<IActionResult> SetIssuePropertyValue(Guid projectId, Guid issueId, Guid propertyId, [FromBody] SetPropertyValueRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var prop = await db.ProjectProperties.FirstOrDefaultAsync(p => p.Id == propertyId && p.ProjectId == projectId);
        if (prop is null) return NotFound();

        var existing = await db.IssuePropertyValues.FirstOrDefaultAsync(v => v.IssueId == issueId && v.PropertyId == propertyId);
        if (existing is null)
        {
            existing = new IssuePropertyValue
            {
                Id = Guid.NewGuid(),
                IssueId = issueId,
                PropertyId = propertyId,
                Value = req.Value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.IssuePropertyValues.Add(existing);
        }
        else
        {
            existing.Value = req.Value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        return Ok(existing);
    }
}

public record CreatePropertyRequest(string Name, ProjectPropertyType Type, bool IsRequired, string? DefaultValue, string? AllowedValues);
public record UpdatePropertyRequest(string Name, ProjectPropertyType Type, bool IsRequired, string? DefaultValue, string? AllowedValues);
public record SetPropertyValueRequest(string? Value);
