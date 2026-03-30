using IssuePit.Notes.Api.Services;
using IssuePit.Notes.Core.Data;
using IssuePit.Notes.Core.Entities;
using IssuePit.Notes.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Notes.Api.Controllers;

[ApiController]
[Route("api/notes/notebooks")]
public class NotebooksController(NotesDbContext db, NotesTenantContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetNotebooks()
    {
        if (ctx.TenantId is null) return Unauthorized();
        var notebooks = await db.Notebooks
            .Include(n => n.Tags)
            .Where(n => n.TenantId == ctx.TenantId.Value)
            .OrderBy(n => n.Name)
            .ToListAsync();
        return Ok(notebooks);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetNotebook(Guid id)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var notebook = await db.Notebooks
            .Include(n => n.Tags)
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == ctx.TenantId.Value);
        return notebook is null ? NotFound() : Ok(notebook);
    }

    [HttpPost]
    public async Task<IActionResult> CreateNotebook([FromBody] CreateNotebookRequest req)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var notebook = new Notebook
        {
            Id = Guid.NewGuid(),
            TenantId = ctx.TenantId.Value,
            Name = req.Name,
            Description = req.Description,
            ProjectId = req.ProjectId,
            StorageProvider = req.StorageProvider,
            GitRepoUrl = req.GitRepoUrl,
            GitBranch = req.GitBranch,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Notebooks.Add(notebook);
        await db.SaveChangesAsync();
        return Created($"/api/notes/notebooks/{notebook.Id}", notebook);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateNotebook(Guid id, [FromBody] UpdateNotebookRequest req)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var notebook = await db.Notebooks.FirstOrDefaultAsync(n => n.Id == id && n.TenantId == ctx.TenantId.Value);
        if (notebook is null) return NotFound();
        notebook.Name = req.Name;
        notebook.Description = req.Description;
        notebook.ProjectId = req.ProjectId;
        notebook.StorageProvider = req.StorageProvider;
        notebook.GitRepoUrl = req.GitRepoUrl;
        notebook.GitBranch = req.GitBranch;
        notebook.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(notebook);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNotebook(Guid id)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var notebook = await db.Notebooks.FirstOrDefaultAsync(n => n.Id == id && n.TenantId == ctx.TenantId.Value);
        if (notebook is null) return NotFound();
        db.Notebooks.Remove(notebook);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Tags ──────────────────────────────────────────────────────────────

    [HttpPost("{notebookId:guid}/tags")]
    public async Task<IActionResult> CreateTag(Guid notebookId, [FromBody] CreateNoteTagRequest req)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var notebookExists = await db.Notebooks.AnyAsync(n => n.Id == notebookId && n.TenantId == ctx.TenantId.Value);
        if (!notebookExists) return NotFound();
        var tag = new NoteTag
        {
            Id = Guid.NewGuid(),
            NotebookId = notebookId,
            Name = req.Name,
            Color = req.Color ?? "#6b7280"
        };
        db.NoteTags.Add(tag);
        await db.SaveChangesAsync();
        return Created($"/api/notes/notebooks/{notebookId}/tags/{tag.Id}", tag);
    }

    [HttpPut("{notebookId:guid}/tags/{id:guid}")]
    public async Task<IActionResult> UpdateTag(Guid notebookId, Guid id, [FromBody] UpdateNoteTagRequest req)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var notebookExists = await db.Notebooks.AnyAsync(n => n.Id == notebookId && n.TenantId == ctx.TenantId.Value);
        if (!notebookExists) return NotFound();
        var tag = await db.NoteTags.FirstOrDefaultAsync(t => t.Id == id && t.NotebookId == notebookId);
        if (tag is null) return NotFound();
        tag.Name = req.Name;
        tag.Color = req.Color ?? tag.Color;
        await db.SaveChangesAsync();
        return Ok(tag);
    }

    [HttpDelete("{notebookId:guid}/tags/{id:guid}")]
    public async Task<IActionResult> DeleteTag(Guid notebookId, Guid id)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var notebookExists = await db.Notebooks.AnyAsync(n => n.Id == notebookId && n.TenantId == ctx.TenantId.Value);
        if (!notebookExists) return NotFound();
        var tag = await db.NoteTags.FirstOrDefaultAsync(t => t.Id == id && t.NotebookId == notebookId);
        if (tag is null) return NotFound();
        db.NoteTags.Remove(tag);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateNotebookRequest(
    string Name,
    string? Description,
    Guid? ProjectId,
    StorageProvider StorageProvider,
    string? GitRepoUrl,
    string? GitBranch);

public record UpdateNotebookRequest(
    string Name,
    string? Description,
    Guid? ProjectId,
    StorageProvider StorageProvider,
    string? GitRepoUrl,
    string? GitBranch);

public record CreateNoteTagRequest(string Name, string? Color);
public record UpdateNoteTagRequest(string Name, string? Color);
