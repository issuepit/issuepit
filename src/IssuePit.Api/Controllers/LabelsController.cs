using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/labels")]
public class LabelsController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLabels(Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var labels = await db.Labels.Where(l => l.ProjectId == projectId).ToListAsync();
        return Ok(labels);
    }

    [HttpPost]
    public async Task<IActionResult> CreateLabel(Guid projectId, [FromBody] LabelRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var label = new Label
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = req.Name,
            Color = req.Color,
        };
        db.Labels.Add(label);
        await db.SaveChangesAsync();
        return Created($"/api/projects/{projectId}/labels/{label.Id}", label);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateLabel(Guid projectId, Guid id, [FromBody] LabelRequest req)
    {
        var label = await db.Labels.FirstOrDefaultAsync(l => l.Id == id && l.ProjectId == projectId);
        if (label is null) return NotFound();
        label.Name = req.Name;
        label.Color = req.Color;
        await db.SaveChangesAsync();
        return Ok(label);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLabel(Guid projectId, Guid id)
    {
        var label = await db.Labels.FirstOrDefaultAsync(l => l.Id == id && l.ProjectId == projectId);
        if (label is null) return NotFound();
        db.Labels.Remove(label);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record LabelRequest(string Name, string Color);
