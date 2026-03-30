using System.Net;
using System.Net.Http.Json;
using IssuePit.Notes.Core.Data;
using IssuePit.Notes.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class NotesEndpointTests(NotesApiFactory factory) : IClassFixture<NotesApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly Guid TestTenantId = Guid.NewGuid();

    private async Task<Guid> SeedWorkspaceAsync(string name = "My Notes")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
        var workspace = new NoteWorkspace
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            Name = name
        };
        db.NoteWorkspaces.Add(workspace);
        await db.SaveChangesAsync();
        return workspace.Id;
    }

    private async Task<Guid> SeedNoteAsync(Guid workspaceId, string title = "Test Note", string content = "Hello world")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
        var note = new Note
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            WorkspaceId = workspaceId,
            Title = title,
            Content = content,
            Version = 1
        };
        db.Notes.Add(note);
        await db.SaveChangesAsync();
        return note.Id;
    }

    // ── Workspaces ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetWorkspaces_Returns200()
    {
        await SeedWorkspaceAsync("Work Notes");
        SetTenantHeader();

        var response = await _client.GetAsync("/api/notes/workspaces");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task GetWorkspaces_WithoutTenant_Returns401()
    {
        var response = await _client.GetAsync("/api/notes/workspaces");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateWorkspace_Returns201()
    {
        SetTenantHeader();

        var response = await _client.PostAsJsonAsync("/api/notes/workspaces",
            new { name = "Project Notes", description = "Notes for my project" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task GetWorkspace_Returns200()
    {
        var wsId = await SeedWorkspaceAsync("Detail Test");
        SetTenantHeader();

        var response = await _client.GetAsync($"/api/notes/workspaces/{wsId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task UpdateWorkspace_Returns200()
    {
        var wsId = await SeedWorkspaceAsync("OldName");
        SetTenantHeader();

        var response = await _client.PutAsJsonAsync($"/api/notes/workspaces/{wsId}",
            new { name = "NewName" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task DeleteWorkspace_Returns204()
    {
        var wsId = await SeedWorkspaceAsync("ToDelete");
        SetTenantHeader();

        var response = await _client.DeleteAsync($"/api/notes/workspaces/{wsId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        RemoveTenantHeader();
    }

    // ── Notes ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNotes_Returns200()
    {
        var wsId = await SeedWorkspaceAsync("Notes List");
        await SeedNoteAsync(wsId, "First Note");
        SetTenantHeader();

        var response = await _client.GetAsync($"/api/notes/workspace/{wsId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task CreateNote_Returns201()
    {
        var wsId = await SeedWorkspaceAsync("Create Test");
        SetTenantHeader();

        var response = await _client.PostAsJsonAsync("/api/notes",
            new { workspaceId = wsId, title = "New Note", content = "Some markdown content" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task CreateNote_WithWikiLinks_ExtractsLinks()
    {
        var wsId = await SeedWorkspaceAsync("Link Test");
        SetTenantHeader();

        var response = await _client.PostAsJsonAsync("/api/notes",
            new { workspaceId = wsId, title = "Linked Note", content = "Check [[Other Note]] and [[issue:123]]" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Fetch the created note to verify links
        var noteResponse = await response.Content.ReadFromJsonAsync<NoteDetailDto>();
        Assert.NotNull(noteResponse);

        var getResponse = await _client.GetAsync($"/api/notes/{noteResponse!.Id}");
        var detail = await getResponse.Content.ReadFromJsonAsync<NoteDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal(2, detail!.Links.Count);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task GetNote_Returns200()
    {
        var wsId = await SeedWorkspaceAsync("Get Test");
        var noteId = await SeedNoteAsync(wsId, "Read Me");
        SetTenantHeader();

        var response = await _client.GetAsync($"/api/notes/{noteId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task UpdateNote_Returns200_IncreasesVersion()
    {
        var wsId = await SeedWorkspaceAsync("Update Test");
        var noteId = await SeedNoteAsync(wsId, "V1 Note");
        SetTenantHeader();

        var response = await _client.PutAsJsonAsync($"/api/notes/{noteId}",
            new { title = "V2 Note", content = "Updated content", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<NoteDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal(2, detail!.Version);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task UpdateNote_WrongVersion_Returns409()
    {
        var wsId = await SeedWorkspaceAsync("Conflict Test");
        var noteId = await SeedNoteAsync(wsId, "Conflict Note");
        SetTenantHeader();

        var response = await _client.PutAsJsonAsync($"/api/notes/{noteId}",
            new { title = "Should Fail", expectedVersion = 999 });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task DeleteNote_Returns204()
    {
        var wsId = await SeedWorkspaceAsync("Delete Test");
        var noteId = await SeedNoteAsync(wsId, "To Delete");
        SetTenantHeader();

        var response = await _client.DeleteAsync($"/api/notes/{noteId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        RemoveTenantHeader();
    }

    // ── Graph ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGraphData_Returns200()
    {
        var wsId = await SeedWorkspaceAsync("Graph Test");
        await SeedNoteAsync(wsId, "Node A");
        await SeedNoteAsync(wsId, "Node B");
        SetTenantHeader();

        var response = await _client.GetAsync($"/api/notes/workspace/{wsId}/graph");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var graph = await response.Content.ReadFromJsonAsync<GraphDataDto>();
        Assert.NotNull(graph);
        Assert.Equal(2, graph!.Nodes.Count);

        RemoveTenantHeader();
    }

    // ── Search ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchNotes_Returns200()
    {
        var wsId = await SeedWorkspaceAsync("Search Test");
        await SeedNoteAsync(wsId, "Alpha Note", "Contains alpha keyword");
        await SeedNoteAsync(wsId, "Beta Note", "Different content");
        SetTenantHeader();

        var response = await _client.GetAsync($"/api/notes/workspace/{wsId}?search=alpha");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetTenantHeader()
    {
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", TestTenantId.ToString());
    }

    private void RemoveTenantHeader() =>
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");

    // ── DTOs for deserialization ───────────────────────────────────────────

    private record NoteDetailDto(Guid Id, Guid WorkspaceId, string Title, string Content, int Version,
        DateTime CreatedAt, DateTime UpdatedAt, List<NoteLinkDto> Links);

    private record NoteLinkDto(Guid Id, int LinkType, Guid? TargetNoteId, Guid? TargetEntityId, string? RawLinkText);

    private record GraphDataDto(List<GraphNodeDto> Nodes, List<GraphEdgeDto> Edges);

    private record GraphNodeDto(Guid Id, string Title);

    private record GraphEdgeDto(Guid SourceId, int LinkType, Guid? TargetNoteId, Guid? TargetEntityId, string? RawLinkText);
}
