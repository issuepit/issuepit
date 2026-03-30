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
    [HttpGet]
    public async Task<IActionResult> GetNotes(
        [FromQuery] Guid? notebookId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] Guid? tagId)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var query = db.Notes
            .Include(n => n.TagMappings).ThenInclude(m => m.Tag)
            .Include(n => n.OutgoingLinks)
            .Where(n => n.TenantId == ctx.TenantId.Value);

        if (notebookId.HasValue)
            query = query.Where(n => n.NotebookId == notebookId.Value);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<NoteStatus>(status, true, out var s))
            query = query.Where(n => n.Status == s);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(n => n.Title.Contains(search) || n.Content.Contains(search));

        if (tagId.HasValue)
            query = query.Where(n => n.TagMappings.Any(m => m.TagId == tagId.Value));

        var notes = await query.OrderByDescending(n => n.UpdatedAt).ToListAsync();
        return Ok(notes);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetNote(Guid id)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var note = await db.Notes
            .Include(n => n.TagMappings).ThenInclude(m => m.Tag)
            .Include(n => n.OutgoingLinks)
            .Include(n => n.IncomingLinks)
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == ctx.TenantId.Value);
        return note is null ? NotFound() : Ok(note);
    }

    [HttpPost]
    public async Task<IActionResult> CreateNote([FromBody] CreateNoteRequest req)
    {
        if (ctx.TenantId is null) return Unauthorized();

        var notebookExists = await db.Notebooks.AnyAsync(n => n.Id == req.NotebookId && n.TenantId == ctx.TenantId.Value);
        if (!notebookExists) return BadRequest("Notebook not found.");

        var slug = GenerateSlug(req.Title);
        var note = new Note
        {
            Id = Guid.NewGuid(),
            TenantId = ctx.TenantId.Value,
            NotebookId = req.NotebookId,
            Title = req.Title,
            Content = req.Content ?? string.Empty,
            Status = req.Status,
            Slug = slug,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Notes.Add(note);

        // Parse and create wiki-style links from content
        var links = ParseWikiLinks(note.Id, note.Content, note.NotebookId);
        db.NoteLinks.AddRange(links);

        // Tag mappings
        if (req.TagIds is { Count: > 0 })
        {
            foreach (var tagId in req.TagIds)
                db.NoteTagMappings.Add(new NoteTagMapping { NoteId = note.Id, TagId = tagId });
        }

        await db.SaveChangesAsync();
        var created = await db.Notes
            .Include(n => n.TagMappings).ThenInclude(m => m.Tag)
            .Include(n => n.OutgoingLinks)
            .FirstAsync(n => n.Id == note.Id);
        return Created($"/api/notes/{note.Id}", created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateNote(Guid id, [FromBody] UpdateNoteRequest req)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var note = await db.Notes
            .Include(n => n.OutgoingLinks)
            .Include(n => n.TagMappings)
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == ctx.TenantId.Value);
        if (note is null) return NotFound();

        // Optimistic concurrency: reject if the supplied version doesn't match
        if (req.ExpectedVersion.HasValue && req.ExpectedVersion.Value != note.Version)
            return Conflict(new VersionConflictResponse(note.Version, "Note has been modified by another user. Reload and try again."));

        note.Title = req.Title;
        note.Content = req.Content ?? string.Empty;
        note.Status = req.Status;
        note.Slug = GenerateSlug(req.Title);
        note.Version++;
        note.UpdatedAt = DateTime.UtcNow;

        // Re-parse links
        db.NoteLinks.RemoveRange(note.OutgoingLinks);
        var links = ParseWikiLinks(note.Id, note.Content, note.NotebookId);
        db.NoteLinks.AddRange(links);

        // Update tag mappings
        db.NoteTagMappings.RemoveRange(note.TagMappings);
        if (req.TagIds is { Count: > 0 })
        {
            foreach (var tagId in req.TagIds)
                db.NoteTagMappings.Add(new NoteTagMapping { NoteId = note.Id, TagId = tagId });
        }

        await db.SaveChangesAsync();
        var updated = await db.Notes
            .Include(n => n.TagMappings).ThenInclude(m => m.Tag)
            .Include(n => n.OutgoingLinks)
            .FirstAsync(n => n.Id == id);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNote(Guid id)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.TenantId == ctx.TenantId.Value);
        if (note is null) return NotFound();
        db.Notes.Remove(note);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Graph Data ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all notes and their links for graph visualization.
    /// </summary>
    [HttpGet("graph")]
    public async Task<IActionResult> GetGraph([FromQuery] Guid? notebookId)
    {
        if (ctx.TenantId is null) return Unauthorized();
        var notesQuery = db.Notes
            .Where(n => n.TenantId == ctx.TenantId.Value);

        if (notebookId.HasValue)
            notesQuery = notesQuery.Where(n => n.NotebookId == notebookId.Value);

        var notes = await notesQuery
            .Select(n => new GraphNode(n.Id, n.Title, n.Slug, n.NotebookId))
            .ToListAsync();

        var noteIds = notes.Select(n => n.Id).ToHashSet();
        var links = await db.NoteLinks
            .Where(l => noteIds.Contains(l.SourceNoteId))
            .Select(l => new GraphEdge(l.SourceNoteId, l.TargetType, l.TargetNoteId, l.TargetEntityId, l.LinkText))
            .ToListAsync();

        return Ok(new GraphResponse(notes, links));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant().Trim();
        slug = SlugInvalidCharsRegex().Replace(slug, "-");
        slug = SlugMultipleDashRegex().Replace(slug, "-");
        return slug.Trim('-');
    }

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex SlugInvalidCharsRegex();

    [GeneratedRegex(@"[\s-]+")]
    private static partial Regex SlugMultipleDashRegex();

    /// <summary>
    /// Parses [[...]] wiki-style links from markdown content.
    /// </summary>
    private List<NoteLink> ParseWikiLinks(Guid sourceNoteId, string content, Guid notebookId)
    {
        var links = new List<NoteLink>();
        var matches = WikiLinkRegex().Matches(content);
        foreach (Match match in matches)
        {
            var linkText = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(linkText)) continue;

            var link = new NoteLink
            {
                Id = Guid.NewGuid(),
                SourceNoteId = sourceNoteId,
                TargetType = NoteLinkType.Note,
                LinkText = linkText,
            };

            // Try to resolve to an existing note in the same notebook by slug
            var targetSlug = GenerateSlug(linkText);
            var targetNote = db.Notes.FirstOrDefault(n =>
                n.NotebookId == notebookId && n.Slug == targetSlug && n.Id != sourceNoteId);
            if (targetNote is not null)
                link.TargetNoteId = targetNote.Id;

            links.Add(link);
        }
        return links;
    }

    [GeneratedRegex(@"\[\[([^\]]+)\]\]")]
    private static partial Regex WikiLinkRegex();
}

public record CreateNoteRequest(
    Guid NotebookId,
    string Title,
    string? Content,
    NoteStatus Status,
    List<Guid>? TagIds);

public record UpdateNoteRequest(
    string Title,
    string? Content,
    NoteStatus Status,
    List<Guid>? TagIds,
    long? ExpectedVersion);

public record VersionConflictResponse(long CurrentVersion, string Message);

public record GraphNode(Guid Id, string Title, string Slug, Guid NotebookId);
public record GraphEdge(Guid SourceNoteId, NoteLinkType TargetType, Guid? TargetNoteId, Guid? TargetEntityId, string LinkText);
public record GraphResponse(List<GraphNode> Nodes, List<GraphEdge> Edges);
