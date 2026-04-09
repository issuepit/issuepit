using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IssuePit.ExecutionClient.Services;

/// <summary>
/// HTTP client for the Notes API. Used by <see cref="Workers.IssueWorker"/> to create
/// session-summary notes and fetch guideline notes for injection into agent prompts.
/// Requires <c>NotesApi:BaseUrl</c> to be configured; all operations are no-ops when the
/// base URL is not set.
/// </summary>
public class NotesApiClient(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<NotesApiClient> logger)
{
    private const string GuidelinesNotebookName = "Agent Guidelines";
    private const string GuidelinesNotebookDescription = "Auto-generated notebook containing session summaries and guidelines for agent runs.";

    private readonly string? _baseUrl = configuration["NotesApi:BaseUrl"];

    /// <summary>Returns true when the Notes API base URL is configured.</summary>
    public bool IsConfigured => !string.IsNullOrEmpty(_baseUrl);

    /// <summary>
    /// Finds or creates the "Agent Guidelines" notebook for a given project.
    /// Returns the notebook ID, or <c>null</c> when the Notes API is not configured.
    /// </summary>
    public async Task<Guid?> EnsureGuidelinesNotebookAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return null;
        var client = CreateClient(tenantId);

        try
        {
            // Look for an existing notebook linked to this project
            var response = await client.GetAsync("/api/notes/notebooks", cancellationToken);
            response.EnsureSuccessStatusCode();
            var notebooks = await response.Content.ReadFromJsonAsync<List<NotebookDto>>(cancellationToken: cancellationToken);
            var existing = notebooks?.FirstOrDefault(n => n.ProjectId == projectId && n.Name == GuidelinesNotebookName);
            if (existing is not null)
                return existing.Id;

            // Create a new Guidelines notebook for this project
            var createReq = new CreateNotebookDto(GuidelinesNotebookName, GuidelinesNotebookDescription, projectId, 0, null, null);
            var createResponse = await client.PostAsJsonAsync("/api/notes/notebooks", createReq, cancellationToken);
            createResponse.EnsureSuccessStatusCode();
            var created = await createResponse.Content.ReadFromJsonAsync<NotebookDto>(cancellationToken: cancellationToken);
            return created?.Id;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to ensure Guidelines notebook for project {ProjectId}", projectId);
            return null;
        }
    }

    /// <summary>
    /// Creates a summary note in the specified notebook.
    /// Returns the created note's ID, or <c>null</c> on failure.
    /// </summary>
    public async Task<Guid?> CreateSummaryNoteAsync(Guid tenantId, Guid notebookId, string title, string content, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return null;
        var client = CreateClient(tenantId);

        try
        {
            // NoteStatus.Published = 1
            var createReq = new CreateNoteDto(notebookId, title, content, 1, null);
            var response = await client.PostAsJsonAsync("/api/notes", createReq, cancellationToken);
            response.EnsureSuccessStatusCode();
            var note = await response.Content.ReadFromJsonAsync<NoteDto>(cancellationToken: cancellationToken);
            return note?.Id;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create summary note in notebook {NotebookId}", notebookId);
            return null;
        }
    }

    /// <summary>
    /// Fetches recent guideline notes for a project, suitable for injection into an agent's context.
    /// Returns an empty list when the Notes API is not configured or the project has no Guidelines notebook.
    /// </summary>
    public async Task<IReadOnlyList<GuidelineNote>> FetchGuidelineNotesAsync(Guid tenantId, Guid projectId, int maxNotes = 5, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return [];
        var client = CreateClient(tenantId);

        try
        {
            // First, find the Guidelines notebook for the project
            var response = await client.GetAsync("/api/notes/notebooks", cancellationToken);
            response.EnsureSuccessStatusCode();
            var notebooks = await response.Content.ReadFromJsonAsync<List<NotebookDto>>(cancellationToken: cancellationToken);
            var notebook = notebooks?.FirstOrDefault(n => n.ProjectId == projectId && n.Name == GuidelinesNotebookName);
            if (notebook is null) return [];

            // Fetch notes from the Guidelines notebook
            var notesResponse = await client.GetAsync($"/api/notes?notebookId={notebook.Id}&status=Published", cancellationToken);
            notesResponse.EnsureSuccessStatusCode();
            var notes = await notesResponse.Content.ReadFromJsonAsync<List<NoteDto>>(cancellationToken: cancellationToken);
            if (notes is null || notes.Count == 0) return [];

            // Return the most recent notes (Notes API returns ordered by UpdatedAt DESC)
            return notes
                .Take(maxNotes)
                .Select(n => new GuidelineNote(n.Id, n.Title, n.Content))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch guideline notes for project {ProjectId}", projectId);
            return [];
        }
    }

    private HttpClient CreateClient(Guid tenantId)
    {
        var client = httpClientFactory.CreateClient("notes-api");
        client.BaseAddress = new Uri(_baseUrl!);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    // ── DTOs for Notes API communication ─────────────────────────────────

    private sealed record NotebookDto(
        Guid Id,
        Guid TenantId,
        string Name,
        string? Description,
        Guid? ProjectId);

    private sealed record CreateNotebookDto(
        string Name,
        string? Description,
        Guid? ProjectId,
        int StorageProvider,
        string? GitRepoUrl,
        string? GitBranch);

    private sealed record NoteDto(
        Guid Id,
        string Title,
        string Content);

    private sealed record CreateNoteDto(
        Guid NotebookId,
        string Title,
        string? Content,
        int Status,
        List<Guid>? TagIds);
}

/// <summary>A guideline note fetched from the Notes subsystem, ready for injection into an agent prompt.</summary>
public sealed record GuidelineNote(Guid Id, string Title, string Content);
