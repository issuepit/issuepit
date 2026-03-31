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

    // ── Uploads ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadImage_WithoutStorage_Returns503()
    {
        SetTenantHeader();

        using var content = new MultipartFormDataContent();
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header bytes
        content.Add(new ByteArrayContent(imageBytes), "file", "test.png");

        var response = await _client.PostAsync("/api/notes/uploads/image", content);
        // Image storage is not configured in test environment, expect 503
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        RemoveTenantHeader();
    }

    // ── CRDT Operations ───────────────────────────────────────────────────

    [Fact]
    public async Task SubmitOperation_FirstOp_Returns200WithSequence1()
    {
        var notebookId = await SeedNotebookAsync("CrdtNb1");
        var noteId = await SeedNoteAsync(notebookId, "Crdt Note", "hello");
        SetTenantHeader();

        // Insert " world" after "hello" → retain(5), insert(" world")
        var delta = "[{\"retain\":5},{\"insert\":\" world\"}]";
        var response = await _client.PostAsJsonAsync($"/api/notes/{noteId}/operations", new
        {
            delta,
            baseSequence = 0,
            clientId = "test-client-A",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, body.GetProperty("sequenceNumber").GetInt64());

        RemoveTenantHeader();
    }

    [Fact]
    public async Task SubmitOperation_AppliesDeltaToNoteContent()
    {
        var notebookId = await SeedNotebookAsync("CrdtNb2");
        var noteId = await SeedNoteAsync(notebookId, "Content Test", "hello");
        SetTenantHeader();

        // delete "hello" (5 chars) and insert "world"
        var delta = "[{\"delete\":5},{\"insert\":\"world\"}]";
        await _client.PostAsJsonAsync($"/api/notes/{noteId}/operations", new
        {
            delta,
            baseSequence = 0,
            clientId = "test-client-B",
        });

        // Fetch the note and verify content changed
        var noteResp = await _client.GetAsync($"/api/notes/{noteId}");
        var note = await noteResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("world", note.GetProperty("content").GetString());

        RemoveTenantHeader();
    }

    [Fact]
    public async Task SubmitOperation_ConcurrentOps_TransformsAndMerges()
    {
        var notebookId = await SeedNotebookAsync("CrdtNb3");
        var noteId = await SeedNoteAsync(notebookId, "Concurrent", "hello world");
        SetTenantHeader();

        // Op A (client A): insert "dear " at position 6 — retain(6), insert("dear "), retain(5)
        var deltaA = "[{\"retain\":6},{\"insert\":\"dear \"},{\"retain\":5}]";
        var respA = await _client.PostAsJsonAsync($"/api/notes/{noteId}/operations", new
        {
            delta = deltaA,
            baseSequence = 0,
            clientId = "test-client-A",
        });
        Assert.Equal(HttpStatusCode.OK, respA.StatusCode);
        var bodyA = await respA.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var seqA = bodyA.GetProperty("sequenceNumber").GetInt64();

        // Op B (client B): also against base 0 — insert "! " at the end — retain(11), insert("!")
        var deltaB = "[{\"retain\":11},{\"insert\":\"!\"}]";
        var respB = await _client.PostAsJsonAsync($"/api/notes/{noteId}/operations", new
        {
            delta = deltaB,
            baseSequence = 0,   // concurrent: same base as A
            clientId = "test-client-B",
        });
        Assert.Equal(HttpStatusCode.OK, respB.StatusCode);
        var bodyB = await respB.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(bodyB.GetProperty("sequenceNumber").GetInt64() > seqA);

        // After merging, content should include both A's insert and B's insert
        var noteResp = await _client.GetAsync($"/api/notes/{noteId}");
        var note = await noteResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var finalContent = note.GetProperty("content").GetString()!;

        // B appended "!" and A inserted "dear " — both must be present
        Assert.Contains("dear ", finalContent);
        Assert.Contains("!", finalContent);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task GetOperations_ReturnsSince()
    {
        var notebookId = await SeedNotebookAsync("CrdtNb4");
        var noteId = await SeedNoteAsync(notebookId, "Poll Test", "abc");
        SetTenantHeader();

        // Submit two ops
        var delta1 = "[{\"retain\":3},{\"insert\":\"d\"}]";
        await _client.PostAsJsonAsync($"/api/notes/{noteId}/operations", new
        {
            delta = delta1, baseSequence = 0, clientId = "client-X",
        });
        var delta2 = "[{\"retain\":4},{\"insert\":\"e\"}]";
        await _client.PostAsJsonAsync($"/api/notes/{noteId}/operations", new
        {
            delta = delta2, baseSequence = 1, clientId = "client-X",
        });

        // Poll since=0 → should return both ops
        var allOps = await _client.GetAsync($"/api/notes/{noteId}/operations?since=0");
        Assert.Equal(HttpStatusCode.OK, allOps.StatusCode);
        var all = await allOps.Content.ReadFromJsonAsync<System.Text.Json.JsonElement[]>();
        Assert.Equal(2, all!.Length);

        // Poll since=1 → should return only op 2
        var laterOps = await _client.GetAsync($"/api/notes/{noteId}/operations?since=1");
        var later = await laterOps.Content.ReadFromJsonAsync<System.Text.Json.JsonElement[]>();
        Assert.Single(later!);
        Assert.Equal(2, later![0].GetProperty("sequenceNumber").GetInt64());

        RemoveTenantHeader();
    }

    [Fact]
    public async Task SubmitOperation_InvalidDelta_Returns400()
    {
        var notebookId = await SeedNotebookAsync("CrdtNb5");
        var noteId = await SeedNoteAsync(notebookId, "Invalid Delta", "hello");
        SetTenantHeader();

        var response = await _client.PostAsJsonAsync($"/api/notes/{noteId}/operations", new
        {
            delta = "not valid json",
            baseSequence = 0,
            clientId = "test",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        RemoveTenantHeader();
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
