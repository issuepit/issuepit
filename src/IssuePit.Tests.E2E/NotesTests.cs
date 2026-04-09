using System.Net;
using System.Net.Http.Json;
using System.Net.Http;
using System.Text.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the Notes service: verifies notebook and note CRUD through both the API
/// and the Vue UI.  The root cause of the original 401 bug was that the frontend was not
/// passing the <c>X-Tenant-Id</c> header to the Notes API.  These tests confirm:
/// <list type="bullet">
///   <item><description>API: authenticated users can create, list, update, and delete notebooks and notes via the Notes API with the correct X-Tenant-Id header.</description></item>
///   <item><description>UI: the notes page renders, notebooks can be created, and new notes appear in the list.</description></item>
/// </list>
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class NotesTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public NotesTests(AspireFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = "chrome",
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    // ── API Tests ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the <c>X-Tenant-Id</c> header is required: calling the Notes API
    /// without it must return 401.  This is the regression check for the original bug.
    /// </summary>
    [Fact]
    public async Task NotesApi_WithoutTenantIdHeader_Returns401()
    {
        if (_fixture.NotesApiClient is null)
            throw Xunit.Sdk.SkipException.ForSkip("notes-api resource not available.");

        var response = await _fixture.NotesApiClient!.GetAsync("/api/notes/notebooks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Full API happy path: register → get tenant ID from /api/auth/me → create notebook via Notes API
    /// → list notebooks → create note → get note → update note → delete note → delete notebook.
    /// </summary>
    [Fact]
    public async Task Api_Notes_FullCrud()
    {
        if (_fixture.NotesApiClient is null)
            throw Xunit.Sdk.SkipException.ForSkip("notes-api resource not available.");

        // ── 1. Set up authenticated main-API client ──────────────────────────
        using var apiClient = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"nt{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        var reg = await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);

        // ── 2. Verify /api/auth/me returns tenantId ─────────────────────────
        var meResp = await apiClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);
        var me = await meResp.Content.ReadFromJsonAsync<JsonElement>();
        var meTenantId = me.GetProperty("tenantId").GetString();
        Assert.False(string.IsNullOrEmpty(meTenantId), "/api/auth/me should return a tenantId");
        Assert.Equal(tenantId, meTenantId);

        // ── 3. Create a notebook via the Notes API using the tenant ID ───────
        using var notesClient = CreateNotesClient(tenantId);
        var nbResp = await notesClient.PostAsJsonAsync("/api/notes/notebooks", new
        {
            name = $"E2E Notebook {Guid.NewGuid():N}"[..30],
            description = "Created by E2E test",
            storageProvider = "postgres",
        });
        Assert.Equal(HttpStatusCode.Created, nbResp.StatusCode);
        var nb = await nbResp.Content.ReadFromJsonAsync<JsonElement>();
        var notebookId = nb.GetProperty("id").GetString()!;

        // ── 4. List notebooks ────────────────────────────────────────────────
        var listNbResp = await notesClient.GetAsync("/api/notes/notebooks");
        Assert.Equal(HttpStatusCode.OK, listNbResp.StatusCode);
        var notebooks = await listNbResp.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(notebooks);
        Assert.Contains(notebooks, n => n.GetProperty("id").GetString() == notebookId);

        // ── 5. Create a note ─────────────────────────────────────────────────
        var noteResp = await notesClient.PostAsJsonAsync("/api/notes", new
        {
            notebookId = Guid.Parse(notebookId),
            title = "E2E Test Note",
            content = "This is a test note created by E2E tests.",
            status = "draft",
        });
        Assert.Equal(HttpStatusCode.Created, noteResp.StatusCode);
        var note = await noteResp.Content.ReadFromJsonAsync<JsonElement>();
        var noteId = note.GetProperty("id").GetString()!;

        // ── 6. Get the note ──────────────────────────────────────────────────
        var getNoteResp = await notesClient.GetAsync($"/api/notes/{noteId}");
        Assert.Equal(HttpStatusCode.OK, getNoteResp.StatusCode);
        var fetchedNote = await getNoteResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("E2E Test Note", fetchedNote.GetProperty("title").GetString());

        // ── 7. Update the note ───────────────────────────────────────────────
        var updateResp = await notesClient.PutAsJsonAsync($"/api/notes/{noteId}", new
        {
            title = "E2E Test Note (Updated)",
            content = "Updated content.",
            status = "published",
            expectedVersion = 1L,
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updatedNote = await updateResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("E2E Test Note (Updated)", updatedNote.GetProperty("title").GetString());

        // ── 8. List notes ────────────────────────────────────────────────────
        var listNotesResp = await notesClient.GetAsync($"/api/notes?notebookId={notebookId}");
        Assert.Equal(HttpStatusCode.OK, listNotesResp.StatusCode);
        var notes = await listNotesResp.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(notes);
        Assert.Contains(notes, n => n.GetProperty("id").GetString() == noteId);

        // ── 9. Delete the note ───────────────────────────────────────────────
        var deleteNoteResp = await notesClient.DeleteAsync($"/api/notes/{noteId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteNoteResp.StatusCode);

        // ── 10. Update the notebook ─────────────────────────────────────────
        var updateNbResp = await notesClient.PutAsJsonAsync($"/api/notes/notebooks/{notebookId}", new
        {
            name = "E2E Notebook (Updated)",
            description = "Updated description",
            storageProvider = "postgres",
        });
        Assert.Equal(HttpStatusCode.OK, updateNbResp.StatusCode);

        // ── 11. Delete the notebook ──────────────────────────────────────────
        var deleteNbResp = await notesClient.DeleteAsync($"/api/notes/notebooks/{notebookId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteNbResp.StatusCode);
    }

    /// <summary>
    /// Verifies that notebook tags can be created, updated, and deleted.
    /// </summary>
    [Fact]
    public async Task Api_Notes_TagCrud()
    {
        if (_fixture.NotesApiClient is null)
            throw Xunit.Sdk.SkipException.ForSkip("notes-api resource not available.");

        var tenantId = await GetDefaultTenantIdAsync();
        using var notesClient = CreateNotesClient(tenantId);

        // Create notebook
        var nbResp = await notesClient.PostAsJsonAsync("/api/notes/notebooks", new
        {
            name = $"TagTest {Guid.NewGuid():N}"[..20],
            storageProvider = "postgres",
        });
        Assert.Equal(HttpStatusCode.Created, nbResp.StatusCode);
        var nb = await nbResp.Content.ReadFromJsonAsync<JsonElement>();
        var notebookId = nb.GetProperty("id").GetString()!;

        // Create tag
        var tagResp = await notesClient.PostAsJsonAsync($"/api/notes/notebooks/{notebookId}/tags", new
        {
            name = "important",
            color = "#ef4444",
        });
        Assert.Equal(HttpStatusCode.Created, tagResp.StatusCode);
        var tag = await tagResp.Content.ReadFromJsonAsync<JsonElement>();
        var tagId = tag.GetProperty("id").GetString()!;

        // Update tag
        var updateTagResp = await notesClient.PutAsJsonAsync($"/api/notes/notebooks/{notebookId}/tags/{tagId}", new
        {
            name = "critical",
            color = "#dc2626",
        });
        Assert.Equal(HttpStatusCode.OK, updateTagResp.StatusCode);

        // Delete tag
        var deleteTagResp = await notesClient.DeleteAsync($"/api/notes/notebooks/{notebookId}/tags/{tagId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteTagResp.StatusCode);

        // Clean up notebook
        await notesClient.DeleteAsync($"/api/notes/notebooks/{notebookId}");
    }

    /// <summary>
    /// Verifies that the note graph endpoint returns data.
    /// </summary>
    [Fact]
    public async Task Api_Notes_GraphEndpointReturnsData()
    {
        if (_fixture.NotesApiClient is null)
            throw Xunit.Sdk.SkipException.ForSkip("notes-api resource not available.");

        var tenantId = await GetDefaultTenantIdAsync();
        using var notesClient = CreateNotesClient(tenantId);

        // The demo seed should have created some notes; just verify the endpoint returns 200.
        var graphResp = await notesClient.GetAsync("/api/notes/graph");
        Assert.Equal(HttpStatusCode.OK, graphResp.StatusCode);
    }

    // ── UI Tests ──────────────────────────────────────────────────────────────

    /// <summary>
    /// UI smoke test: log in as the demo "alice" user, navigate to /notes, verify the page loads
    /// with the seeded notebooks and notes visible.
    /// </summary>
    [Fact]
    public async Task Ui_Notes_SeededDataVisible()
    {
        if (FrontendUrl is null)
            throw Xunit.Sdk.SkipException.ForSkip("Frontend URL not available.");

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = FrontendUrl,
        });
        context.SetDefaultTimeout(E2ETimeouts.Default);
        var page = await context.NewPageAsync();

        try
        {
            // Log in as alice (seeded demo user)
            await LoginAsync(page, "alice", "alice");

            var notesPage = new NotesPage(page);
            await notesPage.GotoAsync();

            // The page should load without showing a login redirect
            Assert.Contains("/notes", page.Url);

            // After the seeder runs, at least the "Engineering" notebook should be listed
            // in the notebook selector. The <select> is rendered only when notebooks exist
            // (v-if="store.notebooks.length"), so wait for the select element to be visible
            // first, then check the option count. <option> elements inside a closed <select>
            // are never "visible" in Playwright, so we use CountAsync instead of WaitForSelector.
            await page.WaitForSelectorAsync("select", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });

            var hasEngineering = await page.Locator("select option:has-text('Engineering')").CountAsync() > 0;
            Assert.True(hasEngineering, "Engineering notebook should be visible in the notebook selector");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// UI smoke test: create a notebook and a note through the UI, verify the note appears in the list.
    /// </summary>
    [Fact]
    public async Task Ui_Notes_CreateNotebookAndNote()
    {
        if (FrontendUrl is null)
            throw Xunit.Sdk.SkipException.ForSkip("Frontend URL not available.");

        // Register a fresh user so there are no pre-existing notes
        using var apiClient = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        var username = $"uinotes{Guid.NewGuid():N}"[..14];
        const string password = "TestPass1!";
        await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password });

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = FrontendUrl,
        });
        context.SetDefaultTimeout(E2ETimeouts.Default);
        var page = await context.NewPageAsync();

        try
        {
            await LoginAsync(page, username, password);

            var notesPage = new NotesPage(page);
            await notesPage.GotoAsync();

            // Create a notebook
            var notebookName = $"UITestNotebook {Guid.NewGuid():N}"[..24];
            await notesPage.CreateNotebookAsync(notebookName);

            // Close modal and create a note
            await notesPage.CloseNotebooksModalAsync();

            var noteTitle = $"UITestNote {Guid.NewGuid():N}"[..20];
            await notesPage.CreateNoteAsync(noteTitle, notebookName);

            // Go back to the notes list and verify the note appears
            await notesPage.GotoAsync();

            // The note list might require selecting the notebook
            await page.SelectOptionAsync("select", new SelectOptionValue { Label = notebookName });
            await page.WaitForTimeoutAsync(500); // brief wait for list refresh

            var noteExists = await notesPage.NoteExistsInListAsync(noteTitle);
            Assert.True(noteExists, $"Note '{noteTitle}' should appear in the notes list");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient CreateCookieClient()
    {
        var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
        return new HttpClient(handler) { BaseAddress = _fixture.ApiClient!.BaseAddress };
    }

    private HttpClient CreateNotesClient(string tenantId)
    {
        var client = new HttpClient { BaseAddress = _fixture.NotesApiClient!.BaseAddress };
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        return client;
    }

    private async Task<string> GetDefaultTenantIdAsync()
    {
        var resp = await _fixture.ApiClient!.GetAsync("/api/admin/tenants");
        resp.EnsureSuccessStatusCode();
        var tenants = await resp.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var tenant in tenants.EnumerateArray())
        {
            if (tenant.GetProperty("hostname").GetString() == "localhost")
                return tenant.GetProperty("id").GetString()!;
        }
        throw new InvalidOperationException("Default 'localhost' tenant not found.");
    }

    private static async Task LoginAsync(IPage page, string username, string password)
    {
        await new LoginPage(page).LoginAsync(username, password);
    }
}
