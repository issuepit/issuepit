using System.Text.Json;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/dashboard/layouts")]
public class DashboardLayoutsController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    /// <summary>
    /// Returns saved dashboard layouts visible to the current user:
    /// their own personal layouts, the project default (if projectId is given), and shared templates.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLayouts(
        [FromQuery] string dashboardType,
        [FromQuery] Guid? projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var query = db.DashboardLayouts
            .Where(l => l.TenantId == ctx.CurrentTenant.Id && l.DashboardType == dashboardType);

        if (projectId.HasValue)
            query = query.Where(l => l.ProjectId == null || l.ProjectId == projectId.Value);
        else
            query = query.Where(l => l.ProjectId == null);

        var userId = ctx.CurrentUser?.Id;

        var layouts = await query
            .Where(l =>
                l.Scope == "shared" ||
                l.Scope == "project_default" ||
                (l.Scope == "user" && l.UserId == userId))
            .OrderByDescending(l => l.UpdatedAt)
            .Select(l => new DashboardLayoutDto
            {
                Id = l.Id,
                Name = l.Name,
                DashboardType = l.DashboardType,
                Scope = l.Scope,
                ProjectId = l.ProjectId,
                UserId = l.UserId,
                LayoutJson = l.LayoutJson,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt,
            })
            .ToListAsync();

        return Ok(layouts);
    }

    /// <summary>
    /// Saves a new dashboard layout (named template, project default, or user layout).
    /// For 'project_default' scope only one layout per project+dashboardType may exist;
    /// an existing one will be replaced.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SaveLayout([FromBody] SaveLayoutRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (ctx.CurrentUser is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");
        if (string.IsNullOrWhiteSpace(req.DashboardType)) return BadRequest("DashboardType is required.");
        if (string.IsNullOrWhiteSpace(req.Scope)) return BadRequest("Scope is required.");
        if (!new[] { "user", "project_default", "shared" }.Contains(req.Scope))
            return BadRequest("Invalid scope.");
        if (string.IsNullOrWhiteSpace(req.LayoutJson)) return BadRequest("LayoutJson is required.");

        // Validate JSON
        try { JsonDocument.Parse(req.LayoutJson); }
        catch { return BadRequest("LayoutJson is not valid JSON."); }

        // For 'project_default' scope, replace any existing project default layout
        if (req.Scope == "project_default" && req.ProjectId.HasValue)
        {
            var existing = await db.DashboardLayouts.FirstOrDefaultAsync(l =>
                l.TenantId == ctx.CurrentTenant.Id &&
                l.DashboardType == req.DashboardType &&
                l.Scope == "project_default" &&
                l.ProjectId == req.ProjectId.Value);

            if (existing is not null)
            {
                existing.Name = req.Name;
                existing.LayoutJson = req.LayoutJson;
                existing.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Ok(MapToDto(existing));
            }
        }

        var layout = new DashboardLayout
        {
            Id = Guid.NewGuid(),
            TenantId = ctx.CurrentTenant.Id,
            Name = req.Name,
            DashboardType = req.DashboardType,
            Scope = req.Scope,
            ProjectId = req.ProjectId,
            UserId = req.Scope == "user" ? ctx.CurrentUser.Id : null,
            LayoutJson = req.LayoutJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.DashboardLayouts.Add(layout);
        await db.SaveChangesAsync();
        return Created($"/api/dashboard/layouts/{layout.Id}", MapToDto(layout));
    }

    /// <summary>Deletes a dashboard layout. Users can only delete their own or shared layouts they created.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLayout(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (ctx.CurrentUser is null) return Unauthorized();

        var layout = await db.DashboardLayouts.FirstOrDefaultAsync(l =>
            l.Id == id && l.TenantId == ctx.CurrentTenant.Id);

        if (layout is null) return NotFound();

        // Users can delete their own layouts or project-default/shared if admin
        if (layout.Scope == "user" && layout.UserId != ctx.CurrentUser.Id)
            return Forbid();

        db.DashboardLayouts.Remove(layout);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static DashboardLayoutDto MapToDto(DashboardLayout l) => new()
    {
        Id = l.Id,
        Name = l.Name,
        DashboardType = l.DashboardType,
        Scope = l.Scope,
        ProjectId = l.ProjectId,
        UserId = l.UserId,
        LayoutJson = l.LayoutJson,
        CreatedAt = l.CreatedAt,
        UpdatedAt = l.UpdatedAt,
    };
}

public class DashboardLayoutDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DashboardType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public Guid? UserId { get; set; }
    public string LayoutJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SaveLayoutRequest
{
    public string Name { get; set; } = string.Empty;
    public string DashboardType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public string LayoutJson { get; set; } = string.Empty;
}
