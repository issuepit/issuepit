using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/custom-properties")]
public class CustomPropertiesController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCustomProperties(Guid projectId)
    {
        var props = await db.CustomProperties
            .Where(p => p.ProjectId == projectId)
            .OrderBy(p => p.Position)
            .ToListAsync();
        return Ok(props);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCustomProperty(Guid projectId, [FromBody] CreateCustomPropertyRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var position = await db.CustomProperties.CountAsync(p => p.ProjectId == projectId);
        var prop = new CustomProperty
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = req.Name,
            Type = req.Type,
            IsRequired = req.IsRequired,
            DefaultValue = req.DefaultValue,
            AllowedValues = req.AllowedValues,
            Position = position,
            CreatedAt = DateTime.UtcNow,
        };
        db.CustomProperties.Add(prop);
        await db.SaveChangesAsync();
        return Created($"/api/projects/{projectId}/custom-properties/{prop.Id}", prop);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCustomProperty(Guid projectId, Guid id, [FromBody] UpdateCustomPropertyRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var prop = await db.CustomProperties.FirstOrDefaultAsync(p => p.Id == id && p.ProjectId == projectId);
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
    public async Task<IActionResult> DeleteCustomProperty(Guid projectId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var prop = await db.CustomProperties.FirstOrDefaultAsync(p => p.Id == id && p.ProjectId == projectId);
        if (prop is null) return NotFound();
        db.CustomProperties.Remove(prop);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/issues/{issueId:guid}/property-values")]
public class IssuePropertyValuesController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetValues(Guid issueId)
    {
        var values = await db.IssuePropertyValues
            .Include(v => v.CustomProperty)
            .Where(v => v.IssueId == issueId)
            .ToListAsync();
        return Ok(values);
    }

    [HttpPut("{customPropertyId:guid}")]
    public async Task<IActionResult> SetValue(Guid issueId, Guid customPropertyId, [FromBody] SetPropertyValueRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var existing = await db.IssuePropertyValues
            .FirstOrDefaultAsync(v => v.IssueId == issueId && v.CustomPropertyId == customPropertyId);
        if (existing is null)
        {
            existing = new IssuePropertyValue
            {
                Id = Guid.NewGuid(),
                IssueId = issueId,
                CustomPropertyId = customPropertyId,
                Value = req.Value,
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

public record CreateCustomPropertyRequest(string Name, CustomPropertyType Type, bool IsRequired, string? DefaultValue, string? AllowedValues);
public record UpdateCustomPropertyRequest(string Name, CustomPropertyType Type, bool IsRequired, string? DefaultValue, string? AllowedValues);
public record SetPropertyValueRequest(string? Value);
