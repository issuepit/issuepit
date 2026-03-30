using System.Text.RegularExpressions;
using IssuePit.Notes.Api.Services;
using IssuePit.Notes.Core.Data;
using IssuePit.Notes.Core.Entities;
using IssuePit.Notes.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Notes.Api.Controllers;

[ApiController]
[Route("api/notes")]
public partial class NotesController(NotesDbContext db, NotesTenantContext ctx) : ControllerBase
{
    [HttpGet("workspace/{workspaceId:guid}")]
    public async Task<IActionResult> GetNotes(Guid workspaceId, [FromQuery] string? search)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var query = db.Notes
            .Where(n => n.TenantId == ctx.TenantId.Value && n.WorkspaceId == workspaceId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(n => n.Title.Contains(search) || n.Content.Contains(search));

        var notes = await query
            .OrderByDescending(n => n.UpdatedAt)
            .Select(n => new NoteListResponse(n.Id, n.WorkspaceId, n.Title, n.Version, n.CreatedAt, n.UpdatedAt))
            .ToListAsync();
        return Ok(notes);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetNote(Guid id)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var note = await db.Notes
            .Include(n => n.OutgoingLinks)
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == ctx.TenantId.Value);
        if (note is null) return NotFound();
        return Ok(new NoteDetailResponse(
            note.Id, note.WorkspaceId, note.Title, note.Content, note.Version,
            note.CreatedAt, note.UpdatedAt,
            note.OutgoingLinks.Select(l => new NoteLinkResponse(
                l.Id, l.LinkType, l.TargetNoteId, l.TargetEntityId, l.RawLinkText)).ToList()));
    }

    [HttpPost]
    public async Task<IActionResult> CreateNote([FromBody] CreateNoteRequest req)
    {
        if (ctx.TenantId is null) return Unauthorized();

        // Verify workspace belongs to this tenant
        var workspaceExists = await db.NoteWorkspaces
            .AnyAsync(w => w.Id == req.WorkspaceId && w.TenantId == ctx.TenantId.Value);
        if (!workspaceExists) return BadRequest("Workspace not found.");

        var note = new Note
        {
            Id = Guid.NewGuid(),
            TenantId = ctx.TenantId.Value,
            WorkspaceId = req.WorkspaceId,
            Title = req.Title,
            Content = req.Content ?? string.Empty,
            Version = 1
        };
        db.Notes.Add(note);

        // Extract and persist wiki-style links from content
        if (!string.IsNullOrEmpty(req.Content))
            ExtractAndAddLinks(note, req.Content);

        await db.SaveChangesAsync();

        return Created($"/api/notes/{note.Id}", new NoteDetailResponse(
            note.Id, note.WorkspaceId, note.Title, note.Content, note.Version,
            note.CreatedAt, note.UpdatedAt, []));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateNote(Guid id, [FromBody] UpdateNoteRequest req)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var note = await db.Notes
            .Include(n => n.OutgoingLinks)
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == ctx.TenantId.Value);
        if (note is null) return NotFound();

        // Optimistic concurrency check
        if (req.ExpectedVersion.HasValue && note.Version != req.ExpectedVersion.Value)
            return Conflict(new VersionConflictResponse(note.Version, req.ExpectedVersion.Value));

        if (req.Title is not null) note.Title = req.Title;
        if (req.Content is not null)
        {
            note.Content = req.Content;

            // Re-extract links when content changes
            db.NoteLinks.RemoveRange(note.OutgoingLinks);
            ExtractAndAddLinks(note, req.Content);
        }
        note.Version++;
        note.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var links = await db.NoteLinks
            .Where(l => l.SourceNoteId == note.Id)
            .Select(l => new NoteLinkResponse(l.Id, l.LinkType, l.TargetNoteId, l.TargetEntityId, l.RawLinkText))
            .ToListAsync();

        return Ok(new NoteDetailResponse(
            note.Id, note.WorkspaceId, note.Title, note.Content, note.Version,
            note.CreatedAt, note.UpdatedAt, links));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNote(Guid id)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var note = await db.Notes
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == ctx.TenantId.Value);
        if (note is null) return NotFound();
        db.Notes.Remove(note);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Graph Data ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all notes and their links within a workspace, suitable for rendering a graph view.
    /// </summary>
    [HttpGet("workspace/{workspaceId:guid}/graph")]
    public async Task<IActionResult> GetGraphData(Guid workspaceId)
    {
        if (ctx.TenantId is null) return Unauthorized();

        var notes = await db.Notes
            .Where(n => n.TenantId == ctx.TenantId.Value && n.WorkspaceId == workspaceId)
            .Select(n => new GraphNode(n.Id, n.Title))
            .ToListAsync();

        var noteIds = notes.Select(n => n.Id).ToHashSet();
        var links = await db.NoteLinks
            .Where(l => noteIds.Contains(l.SourceNoteId))
            .Select(l => new GraphEdge(l.SourceNoteId, l.LinkType, l.TargetNoteId, l.TargetEntityId, l.RawLinkText))
            .ToListAsync();

        return Ok(new GraphDataResponse(notes, links));
    }

    // ── Link Extraction ───────────────────────────────────────────────────

    private void ExtractAndAddLinks(Note note, string content)
    {
        var matches = WikiLinkPattern().Matches(content);
        foreach (Match match in matches)
        {
            var rawText = match.Groups[1].Value.Trim();
            var link = new NoteLink
            {
                Id = Guid.NewGuid(),
                SourceNoteId = note.Id,
                LinkType = DetermineNoteLinkType(rawText),
                RawLinkText = rawText,
                CreatedAt = DateTime.UtcNow
            };

            // Try to resolve target note by title within the same workspace
            if (link.LinkType == NoteLinkType.NoteToNote)
            {
                // Target resolution is deferred — the target note may not exist yet.
                // Graph view and backlink queries will resolve by RawLinkText + title matching.
            }

            db.NoteLinks.Add(link);
        }
    }

    private static NoteLinkType DetermineNoteLinkType(string linkText)
    {
        if (linkText.StartsWith("project:", StringComparison.OrdinalIgnoreCase))
            return NoteLinkType.NoteToProject;
        if (linkText.StartsWith("issue:", StringComparison.OrdinalIgnoreCase))
            return NoteLinkType.NoteToIssue;
        if (linkText.StartsWith("todo:", StringComparison.OrdinalIgnoreCase))
            return NoteLinkType.NoteToTodo;
        return NoteLinkType.NoteToNote;
    }

    [GeneratedRegex(@"\[\[([^\]]+)\]\]")]
    private static partial Regex WikiLinkPattern();
}

// ── Request/Response Records ──────────────────────────────────────────────────

public record NoteListResponse(Guid Id, Guid WorkspaceId, string Title, long Version, DateTime CreatedAt, DateTime UpdatedAt);

public record NoteDetailResponse(
    Guid Id, Guid WorkspaceId, string Title, string Content, long Version,
    DateTime CreatedAt, DateTime UpdatedAt,
    List<NoteLinkResponse> Links);

public record NoteLinkResponse(Guid Id, NoteLinkType LinkType, Guid? TargetNoteId, Guid? TargetEntityId, string? RawLinkText);

public record CreateNoteRequest(Guid WorkspaceId, string Title, string? Content = null);

public record UpdateNoteRequest(string? Title = null, string? Content = null, long? ExpectedVersion = null);

public record VersionConflictResponse(long CurrentVersion, long ExpectedVersion);

public record GraphNode(Guid Id, string Title);

public record GraphEdge(Guid SourceId, NoteLinkType LinkType, Guid? TargetNoteId, Guid? TargetEntityId, string? RawLinkText);

public record GraphDataResponse(List<GraphNode> Nodes, List<GraphEdge> Edges);
