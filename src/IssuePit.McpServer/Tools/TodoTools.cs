using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace IssuePit.McpServer.Tools;

[McpServerToolType]
public class TodoTools(IssuePitApiClient api, IOptions<McpServerOptions> options)
{
    private McpServerOptions Opts => options.Value;

    [McpServerTool, Description("List all todo boards for the current tenant.")]
    public async Task<string> ListTodoBoards(CancellationToken ct = default)
    {
        ToolGuard.EnforceNotEnhanceMode(Opts, "ListTodoBoards");
        var result = await api.GetAsync<object>("/api/todos/boards", ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("List todos, optionally filtered by board, category, or completion status.")]
    public async Task<string> ListTodos(
        [Description("Optional board ID (GUID) to filter todos.")] Guid? boardId = null,
        [Description("Optional category ID (GUID) to filter todos.")] Guid? categoryId = null,
        [Description("Optional filter: true for completed todos, false for incomplete.")] bool? completed = null,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotEnhanceMode(Opts, "ListTodos");
        var qs = new List<string>();
        if (boardId.HasValue) qs.Add($"boardId={boardId.Value}");
        if (categoryId.HasValue) qs.Add($"categoryId={categoryId.Value}");
        if (completed.HasValue) qs.Add($"completed={completed.Value.ToString().ToLower()}");
        var url = "/api/todos" + (qs.Count > 0 ? "?" + string.Join("&", qs) : string.Empty);
        var result = await api.GetAsync<object>(url, ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Get details of a specific todo by its ID.")]
    public async Task<string> GetTodo(
        [Description("The todo ID (GUID).")] Guid id,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotEnhanceMode(Opts, "GetTodo");
        var result = await api.GetAsync<object>($"/api/todos/{id}", ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Create a new todo item.")]
    public async Task<string> CreateTodo(
        [Description("Todo title.")] string title,
        [Description("Optional description (Markdown).")] string? body = null,
        [Description("Priority: no_priority, low, medium, high, urgent.")] string priority = "no_priority",
        [Description("Optional due date in ISO 8601 format (e.g. 2025-12-31T23:59:00Z).")] string? dueDate = null,
        [Description("Optional start date in ISO 8601 format.")] string? startDate = null,
        [Description("Recurring interval: none, daily, weekly, monthly, yearly.")] string recurringInterval = "none",
        [Description("Optional list of board IDs (GUID) to assign the todo to.")] IEnumerable<Guid>? boardIds = null,
        [Description("Optional list of category IDs (GUID) to assign the todo to.")] IEnumerable<Guid>? categoryIds = null,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotReadOnly(Opts, "CreateTodo");
        ToolGuard.EnforceNotEnhanceMode(Opts, "CreateTodo");
        var payload = new
        {
            title,
            body,
            priority,
            dueDate = string.IsNullOrEmpty(dueDate) ? (DateTime?)null : DateTime.Parse(dueDate),
            startDate = string.IsNullOrEmpty(startDate) ? (DateTime?)null : DateTime.Parse(startDate),
            recurringInterval,
            boardIds = boardIds?.ToList(),
            categoryIds = categoryIds?.ToList()
        };
        var result = await api.PostAsync<object>("/api/todos", payload, ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Update an existing todo item.")]
    public async Task<string> UpdateTodo(
        [Description("The todo ID (GUID).")] Guid id,
        [Description("New title.")] string title,
        [Description("New description (Markdown).")] string? body = null,
        [Description("New priority: no_priority, low, medium, high, urgent.")] string priority = "no_priority",
        [Description("New due date in ISO 8601 format.")] string? dueDate = null,
        [Description("New start date in ISO 8601 format.")] string? startDate = null,
        [Description("New recurring interval: none, daily, weekly, monthly, yearly.")] string recurringInterval = "none",
        [Description("Whether the todo is completed.")] bool isCompleted = false,
        [Description("Board IDs (GUID) to assign the todo to.")] IEnumerable<Guid>? boardIds = null,
        [Description("Category IDs (GUID) to assign the todo to.")] IEnumerable<Guid>? categoryIds = null,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotReadOnly(Opts, "UpdateTodo");
        ToolGuard.EnforceNotEnhanceMode(Opts, "UpdateTodo");
        var payload = new
        {
            title,
            body,
            priority,
            dueDate = string.IsNullOrEmpty(dueDate) ? (DateTime?)null : DateTime.Parse(dueDate),
            startDate = string.IsNullOrEmpty(startDate) ? (DateTime?)null : DateTime.Parse(startDate),
            recurringInterval,
            isCompleted,
            boardIds = boardIds?.ToList(),
            categoryIds = categoryIds?.ToList()
        };
        var result = await api.PutAsync<object>($"/api/todos/{id}", payload, ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Delete a todo by its ID.")]
    public async Task<string> DeleteTodo(
        [Description("The todo ID (GUID).")] Guid id,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotReadOnly(Opts, "DeleteTodo");
        ToolGuard.EnforceDestructive(Opts, "DeleteTodo");
        await api.DeleteAsync($"/api/todos/{id}", ct);
        return "Todo deleted successfully.";
    }
}
