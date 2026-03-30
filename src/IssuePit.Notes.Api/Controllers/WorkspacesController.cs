using IssuePit.Notes.Api.Services;
using IssuePit.Notes.Core.Data;
using IssuePit.Notes.Core.Entities;
using IssuePit.Notes.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Notes.Api.Controllers;

[ApiController]
[Route("api/notes/workspaces")]
public class WorkspacesController(NotesDbContext db, NotesTenantContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetWorkspaces()
    {
        if (ctx.TenantId is null) return Unauthorized();
        var workspaces = await db.NoteWorkspaces
            .Where(w => w.TenantId == ctx.TenantId.Value)
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => new WorkspaceResponse(
                w.Id, w.Name, w.Description, w.StorageEngine,
                w.LinkedProjectId, w.GitRepositoryUrl, w.GitBranch,
                w.CreatedAt, w.UpdatedAt,
                w.Notes.Count))
            .ToListAsync();
        return Ok(workspaces);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetWorkspace(Guid id)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var workspace = await db.NoteWorkspaces
            .Where(w => w.Id == id && w.TenantId == ctx.TenantId.Value)
            .Select(w => new WorkspaceResponse(
                w.Id, w.Name, w.Description, w.StorageEngine,
                w.LinkedProjectId, w.GitRepositoryUrl, w.GitBranch,
                w.CreatedAt, w.UpdatedAt,
                w.Notes.Count))
            .FirstOrDefaultAsync();
        return workspace is null ? NotFound() : Ok(workspace);
    }

    [HttpPost]
    public async Task<IActionResult> CreateWorkspace([FromBody] CreateWorkspaceRequest req)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var workspace = new NoteWorkspace
        {
            Id = Guid.NewGuid(),
            TenantId = ctx.TenantId.Value,
            Name = req.Name,
            Description = req.Description,
            StorageEngine = req.StorageEngine ?? NoteStorageEngine.Postgres,
            LinkedProjectId = req.LinkedProjectId,
            GitRepositoryUrl = req.GitRepositoryUrl,
            GitBranch = req.GitBranch
        };
        db.NoteWorkspaces.Add(workspace);
        await db.SaveChangesAsync();
        return Created($"/api/notes/workspaces/{workspace.Id}", new WorkspaceResponse(
            workspace.Id, workspace.Name, workspace.Description, workspace.StorageEngine,
            workspace.LinkedProjectId, workspace.GitRepositoryUrl, workspace.GitBranch,
            workspace.CreatedAt, workspace.UpdatedAt, 0));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateWorkspace(Guid id, [FromBody] UpdateWorkspaceRequest req)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var workspace = await db.NoteWorkspaces
            .FirstOrDefaultAsync(w => w.Id == id && w.TenantId == ctx.TenantId.Value);
        if (workspace is null) return NotFound();

        workspace.Name = req.Name ?? workspace.Name;
        workspace.Description = req.Description ?? workspace.Description;
        workspace.LinkedProjectId = req.LinkedProjectId ?? workspace.LinkedProjectId;
        workspace.GitRepositoryUrl = req.GitRepositoryUrl ?? workspace.GitRepositoryUrl;
        workspace.GitBranch = req.GitBranch ?? workspace.GitBranch;
        workspace.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        var noteCount = await db.Notes.CountAsync(n => n.WorkspaceId == workspace.Id);
        return Ok(new WorkspaceResponse(
            workspace.Id, workspace.Name, workspace.Description, workspace.StorageEngine,
            workspace.LinkedProjectId, workspace.GitRepositoryUrl, workspace.GitBranch,
            workspace.CreatedAt, workspace.UpdatedAt, noteCount));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteWorkspace(Guid id)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var workspace = await db.NoteWorkspaces
            .FirstOrDefaultAsync(w => w.Id == id && w.TenantId == ctx.TenantId.Value);
        if (workspace is null) return NotFound();
        db.NoteWorkspaces.Remove(workspace);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

// ── Request/Response Records ──────────────────────────────────────────────────

public record WorkspaceResponse(
    Guid Id,
    string Name,
    string? Description,
    NoteStorageEngine StorageEngine,
    Guid? LinkedProjectId,
    string? GitRepositoryUrl,
    string? GitBranch,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int NoteCount);

public record CreateWorkspaceRequest(
    string Name,
    string? Description = null,
    NoteStorageEngine? StorageEngine = null,
    Guid? LinkedProjectId = null,
    string? GitRepositoryUrl = null,
    string? GitBranch = null);

public record UpdateWorkspaceRequest(
    string? Name = null,
    string? Description = null,
    Guid? LinkedProjectId = null,
    string? GitRepositoryUrl = null,
    string? GitBranch = null);
