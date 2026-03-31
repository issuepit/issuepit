using System.ComponentModel;
using ModelContextProtocol.Server;

namespace IssuePit.Notes.McpServer.Tools;

[McpServerToolType]
public static class NotesTools
{
    [McpServerTool, Description("List all notebooks in the notes workspace.")]
    public static async Task<string> ListNotebooks(HttpClient client)
    {
        var response = await client.GetAsync("/api/notes/notebooks");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [McpServerTool, Description("List notes, optionally filtered by notebook ID.")]
    public static async Task<string> ListNotes(HttpClient client, string? notebookId = null)
    {
        var url = "/api/notes";
        if (!string.IsNullOrEmpty(notebookId))
            url += $"?notebookId={notebookId}";
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [McpServerTool, Description("Get a single note by its ID, including content and links.")]
    public static async Task<string> GetNote(HttpClient client, string noteId)
    {
        var response = await client.GetAsync($"/api/notes/{noteId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [McpServerTool, Description("Search notes by text query.")]
    public static async Task<string> SearchNotes(HttpClient client, string query, string? notebookId = null)
    {
        var url = $"/api/notes?search={Uri.EscapeDataString(query)}";
        if (!string.IsNullOrEmpty(notebookId))
            url += $"&notebookId={notebookId}";
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [McpServerTool, Description("Get the graph data of note links for visualization.")]
    public static async Task<string> GetNoteGraph(HttpClient client, string? notebookId = null)
    {
        var url = "/api/notes/graph";
        if (!string.IsNullOrEmpty(notebookId))
            url += $"?notebookId={notebookId}";
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
