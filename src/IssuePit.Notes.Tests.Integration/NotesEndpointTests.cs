using System.Net;
using System.Net.Http.Json;
using IssuePit.Notes.Core.Data;
using IssuePit.Notes.Core.Entities;
using IssuePit.Notes.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Notes.Tests.Integration;

[Trait("Category", "Integration")]
public class NotesEndpointTests(NotesApiFactory factory) : IClassFixture<NotesApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly Guid _tenantId = Guid.NewGuid();

    private async Task<Guid> SeedNotebookAsync(string name = "My Notebook")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
        var notebook = new Notebook
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = name,
            StorageProvider = StorageProvider.Postgres,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Notebooks.Add(notebook);
        await db.SaveChangesAsync();
        return notebook.Id;
    }

    private async Task<Guid> SeedNoteAsync(Guid notebookId, string title = "Test Note", string content = "Hello world")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
        var note = new Note
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            NotebookId = notebookId,
            Title = title,
            Content = content,
            Slug = title.ToLowerInvariant().Replace(' ', '-'),
            Status = NoteStatus.Draft,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Notes.Add(note);
        await db.SaveChangesAsync();
        return note.Id;
    }

    // ── Notebooks ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNotebooks_Returns200()
    {
        await SeedNotebookAsync("Work");
        SetTenantHeader();

        var response = await _client.GetAsync("/api/notes/notebooks");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task CreateNotebook_Returns201()
    {
        SetTenantHeader();

        var response = await _client.PostAsJsonAsync("/api/notes/notebooks", new
        {
            name = "Research",
            description = "Research notes",
            storageProvider = "postgres",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task GetNotebook_Returns200()
    {
        var notebookId = await SeedNotebookAsync("Read Me");
        SetTenantHeader();

        var response = await _client.GetAsync($"/api/notes/notebooks/{notebookId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task UpdateNotebook_Returns200()
    {
        var notebookId = await SeedNotebookAsync("OldName");
        SetTenantHeader();

        var response = await _client.PutAsJsonAsync($"/api/notes/notebooks/{notebookId}", new
        {
            name = "NewName",
            description = "Updated",
            storageProvider = "postgres",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task DeleteNotebook_Returns204()
    {
        var notebookId = await SeedNotebookAsync("ToDelete");
        SetTenantHeader();

        var response = await _client.DeleteAsync($"/api/notes/notebooks/{notebookId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        RemoveTenantHeader();
    }

    // ── Tags ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTag_Returns201()
    {
        var notebookId = await SeedNotebookAsync("TagNotebook");
        SetTenantHeader();

        var response = await _client.PostAsJsonAsync($"/api/notes/notebooks/{notebookId}/tags", new
        {
            name = "Important",
            color = "#ef4444",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        RemoveTenantHeader();
    }

    // ── Notes ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNotes_Returns200()
    {
        var notebookId = await SeedNotebookAsync("NotesList");
        await SeedNoteAsync(notebookId, "First Note");
        SetTenantHeader();

        var response = await _client.GetAsync("/api/notes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task CreateNote_Returns201()
    {
        var notebookId = await SeedNotebookAsync("CreateNote");
        SetTenantHeader();

        var response = await _client.PostAsJsonAsync("/api/notes", new
        {
            notebookId,
            title = "My New Note",
            content = "This is a test note with a [[wiki link]].",
            status = "draft",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task GetNote_Returns200()
    {
        var notebookId = await SeedNotebookAsync("GetNote");
        var noteId = await SeedNoteAsync(notebookId, "Detailed Note");
        SetTenantHeader();

        var response = await _client.GetAsync($"/api/notes/{noteId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task UpdateNote_Returns200()
    {
        var notebookId = await SeedNotebookAsync("UpdateNote");
        var noteId = await SeedNoteAsync(notebookId, "Old Title");
        SetTenantHeader();

        var response = await _client.PutAsJsonAsync($"/api/notes/{noteId}", new
        {
            title = "Updated Title",
            content = "Updated content",
            status = "published",
            expectedVersion = 1L,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task UpdateNote_VersionConflict_Returns409()
    {
        var notebookId = await SeedNotebookAsync("ConflictNote");
        var noteId = await SeedNoteAsync(notebookId, "Conflict Test");
        SetTenantHeader();

        // First update increments version to 2
        await _client.PutAsJsonAsync($"/api/notes/{noteId}", new
        {
            title = "First Update",
            content = "content",
            status = "draft",
            expectedVersion = 1L,
        });

        // Second update with stale version should conflict
        var response = await _client.PutAsJsonAsync($"/api/notes/{noteId}", new
        {
            title = "Conflicting Update",
            content = "conflicting content",
            status = "draft",
            expectedVersion = 1L,
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task DeleteNote_Returns204()
    {
        var notebookId = await SeedNotebookAsync("DeleteNote");
        var noteId = await SeedNoteAsync(notebookId, "ToDelete");
        SetTenantHeader();

        var response = await _client.DeleteAsync($"/api/notes/{noteId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        RemoveTenantHeader();
    }

    // ── Graph ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGraph_Returns200()
    {
        var notebookId = await SeedNotebookAsync("GraphNotebook");
        await SeedNoteAsync(notebookId, "Node A", "Links to [[Node B]]");
        await SeedNoteAsync(notebookId, "Node B", "Referenced from A");
        SetTenantHeader();

        var response = await _client.GetAsync("/api/notes/graph");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task GetNotes_FilterByNotebook_Returns200()
    {
        var notebookId = await SeedNotebookAsync("FilterNotebook");
        await SeedNoteAsync(notebookId, "Filtered Note");
        SetTenantHeader();

        var response = await _client.GetAsync($"/api/notes?notebookId={notebookId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task GetNotes_SearchByText_Returns200()
    {
        var notebookId = await SeedNotebookAsync("SearchNotebook");
        await SeedNoteAsync(notebookId, "Searchable Note", "This contains specific search term");
        SetTenantHeader();

        var response = await _client.GetAsync("/api/notes?search=specific");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    // ── Health ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Health_Returns200()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetTenantHeader()
    {
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", _tenantId.ToString());
    }

    private void RemoveTenantHeader() =>
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
}
