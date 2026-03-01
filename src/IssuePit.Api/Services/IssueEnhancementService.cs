using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Enhances an issue using an LLM via OpenRouter by extending its description,
/// creating sub-issues, and creating tasks via an agentic tool-calling loop.
/// The tool set mirrors the MCP server running in enhance mode (issue tools,
/// task tools, and repo file tools only).
/// </summary>
public class IssueEnhancementService(
    IssuePitDbContext db,
    GitService gitService,
    ApiKeyResolverService keyResolver,
    IHttpClientFactory httpClientFactory,
    ILogger<IssueEnhancementService> logger)
{
    private const string OpenRouterBaseUrl = "https://openrouter.ai/api/v1";
    private const string DefaultModel = "anthropic/claude-3.5-sonnet";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyList<object> Tools = BuildToolDefinitions();

    public async Task EnhanceAsync(Guid issueId, CancellationToken ct = default)
    {
        var issue = await db.Issues
            .Include(i => i.Project)
            .ThenInclude(p => p!.Organization)
            .FirstOrDefaultAsync(i => i.Id == issueId, ct)
            ?? throw new InvalidOperationException($"Issue {issueId} not found.");

        var orgId = issue.Project!.OrgId;

        // Resolve the most specific OpenRouter key: project → team → user → org
        var apiKey = await keyResolver.ResolveAsync(
            orgId, ApiKeyProvider.OpenRouter, projectId: issue.ProjectId, ct: ct)
            ?? throw new InvalidOperationException(
                "No OpenRouter API key configured for this organization. " +
                "Add one via POST /api/config/keys with provider 'OpenRouter'.");

        var plainKey = ApiKeyResolverService.DecryptValue(apiKey.EncryptedValue);

        // Inject agents.md content from the git repository if available
        var agentsMd = await ReadAgentsMdAsync(issue.ProjectId, ct);

        var systemPrompt = BuildEnhanceSystemPrompt(agentsMd);
        var userMessage = BuildIssueUserMessage(issue);

        await RunLlmLoopAsync(issue, plainKey, systemPrompt, userMessage, ct);
    }

    private async Task RunLlmLoopAsync(
        Issue issue,
        string apiKey,
        string systemPrompt,
        string userMessage,
        CancellationToken ct)
    {
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userMessage },
        };

        var client = httpClientFactory.CreateClient("openrouter");

        // Cap iterations to prevent runaway loops and limit OpenRouter API costs.
        const int maxIterations = 10;

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var requestBody = new { model = DefaultModel, messages, tools = Tools };
            var json = JsonSerializer.Serialize(requestBody, JsonOptions);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{OpenRouterBaseUrl}/chat/completions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var httpResponse = await client.SendAsync(request, ct);
            httpResponse.EnsureSuccessStatusCode();

            var responseJson = await httpResponse.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<JsonElement>(responseJson, JsonOptions);

            var choice = parsed.GetProperty("choices")[0];
            var assistantMessage = choice.GetProperty("message");
            var finishReason = choice.GetProperty("finish_reason").GetString();

            // Add assistant reply to conversation history
            messages.Add(JsonSerializer.Deserialize<object>(assistantMessage.GetRawText(), JsonOptions)!);

            // Some providers set finish_reason to "tool_calls"; others include tool_calls in the message
            // without updating the finish_reason. Check both to ensure cross-provider compatibility.
            var hasToolCalls = finishReason == "tool_calls"
                || assistantMessage.TryGetProperty("tool_calls", out _);

            if (!hasToolCalls)
            {
                logger.LogInformation("Issue enhance loop finished after {Iterations} iteration(s) for issue {IssueId}.",
                    iteration + 1, issue.Id);
                break;
            }

            var toolCalls = assistantMessage.GetProperty("tool_calls");
            foreach (var toolCall in toolCalls.EnumerateArray())
            {
                var callId = toolCall.GetProperty("id").GetString()!;
                var functionName = toolCall.GetProperty("function").GetProperty("name").GetString()!;
                var argumentsJson = toolCall.GetProperty("function").GetProperty("arguments").GetString()!;

                var result = await ExecuteToolAsync(issue, functionName, argumentsJson, ct);
                messages.Add(new { role = "tool", tool_call_id = callId, content = result });
            }
        }
    }

    private async Task<string> ExecuteToolAsync(Issue issue, string functionName, string argumentsJson, CancellationToken ct)
    {
        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson, JsonOptions);
            return functionName switch
            {
                "update_issue_description" => await UpdateIssueDescriptionAsync(issue, args, ct),
                "create_sub_issue" => await CreateSubIssueAsync(issue, args, ct),
                "create_task" => await CreateTaskAsync(issue, args, ct),
                "get_repo_file" => await GetRepoFileAsync(issue, args, ct),
                "list_repo_files" => await ListRepoFilesAsync(issue, args, ct),
                _ => $"Unknown tool: {functionName}",
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tool call '{Tool}' failed for issue {IssueId}.", functionName, issue.Id);
            return $"Error executing {functionName}: {ex.Message}";
        }
    }

    private async Task<string> UpdateIssueDescriptionAsync(Issue issue, JsonElement args, CancellationToken ct)
    {
        var body = args.GetProperty("body").GetString() ?? string.Empty;
        issue.Body = body;
        issue.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return "Issue description updated successfully.";
    }

    private async Task<string> CreateSubIssueAsync(Issue issue, JsonElement args, CancellationToken ct)
    {
        var title = args.GetProperty("title").GetString() ?? string.Empty;
        var body = args.TryGetProperty("body", out var b) ? b.GetString() : null;
        var priorityStr = args.TryGetProperty("priority", out var p) ? p.GetString() ?? "no_priority" : "no_priority";
        var typeStr = args.TryGetProperty("type", out var t) ? t.GetString() ?? "issue" : "issue";

        var maxNumber = await db.Issues
            .Where(i => i.ProjectId == issue.ProjectId)
            .MaxAsync(i => (int?)i.Number, ct) ?? 0;

        var subIssue = new Issue
        {
            Id = Guid.NewGuid(),
            ProjectId = issue.ProjectId,
            ParentIssueId = issue.Id,
            Title = title,
            Body = body,
            Status = IssueStatus.Backlog,
            Priority = ParsePriority(priorityStr),
            Type = ParseType(typeStr),
            Number = maxNumber + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Issues.Add(subIssue);
        await db.SaveChangesAsync(ct);
        return $"Sub-issue #{subIssue.Number} created: {subIssue.Title}";
    }

    private async Task<string> CreateTaskAsync(Issue issue, JsonElement args, CancellationToken ct)
    {
        var title = args.GetProperty("title").GetString() ?? string.Empty;
        var body = args.TryGetProperty("body", out var b) ? b.GetString() : null;

        var task = new IssueTask
        {
            Id = Guid.NewGuid(),
            IssueId = issue.Id,
            Title = title,
            Body = body,
            Status = IssueStatus.Todo,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.IssueTasks.Add(task);
        await db.SaveChangesAsync(ct);
        return $"Task created: {task.Title}";
    }

    private async Task<string> GetRepoFileAsync(Issue issue, JsonElement args, CancellationToken ct)
    {
        var filePath = args.GetProperty("file_path").GetString() ?? string.Empty;
        var gitRef = args.TryGetProperty("ref", out var r) ? r.GetString() : null;

        var repo = await db.GitRepositories
            .FirstOrDefaultAsync(r => r.ProjectId == issue.ProjectId, ct);

        if (repo is null)
            return "No git repository configured for this project.";

        var blob = await Task.Run(() => gitService.GetBlob(repo, gitRef, filePath), ct);
        if (blob is null)
            return $"File '{filePath}' not found in repository.";

        if (blob.IsBinary)
            return $"File '{filePath}' is binary and cannot be read as text.";

        return blob.Content;
    }

    private async Task<string> ListRepoFilesAsync(Issue issue, JsonElement args, CancellationToken ct)
    {
        var path = args.TryGetProperty("path", out var p) ? p.GetString() : null;
        var gitRef = args.TryGetProperty("ref", out var r) ? r.GetString() : null;

        var repo = await db.GitRepositories
            .FirstOrDefaultAsync(r => r.ProjectId == issue.ProjectId, ct);

        if (repo is null)
            return "No git repository configured for this project.";

        var entries = await Task.Run(() => gitService.GetTree(repo, gitRef, path), ct);
        return JsonSerializer.Serialize(entries, JsonOptions);
    }

    private async Task<string?> ReadAgentsMdAsync(Guid projectId, CancellationToken ct)
    {
        var repo = await db.GitRepositories
            .FirstOrDefaultAsync(r => r.ProjectId == projectId, ct);

        if (repo is null)
            return null;

        try
        {
            var blob = await Task.Run(() => gitService.GetBlob(repo, null, "agents.md"), ct);
            return blob?.IsBinary == false ? blob.Content : null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not read agents.md for project {ProjectId}.", projectId);
            return null;
        }
    }

    private static string BuildEnhanceSystemPrompt(string? agentsMd)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
            You are an AI assistant specializing in software project management and technical planning.
            Your task is to enhance a given software issue by:

            1. Extending the description with implementation details, context, technical guidance, and clear acceptance criteria.
            2. Breaking the issue into specific, independently-trackable sub-issues for each logical component.
            3. Creating concrete tasks that represent the implementation steps.

            Use the available tools to persist your changes. Do not ask for confirmation — proceed directly.

            Guidelines:
            - Sub-issues should represent distinct pieces of work that can be tracked separately.
            - Tasks should be specific, actionable implementation steps (e.g. "Add API endpoint", "Write unit tests").
            - Enhanced descriptions should include: context, acceptance criteria, and technical considerations.
            - Use list_repo_files to explore the repository structure and get_repo_file to read relevant source files for additional context.
            """);

        if (!string.IsNullOrWhiteSpace(agentsMd))
        {
            sb.AppendLine();
            sb.AppendLine("## Project Agent Guidelines (agents.md)");
            sb.AppendLine(agentsMd);
        }

        return sb.ToString().Trim();
    }

    private static string BuildIssueUserMessage(Issue issue)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Issue #{issue.Number}: {issue.Title}");
        sb.AppendLine();
        sb.AppendLine($"**Status:** {issue.Status}");
        sb.AppendLine($"**Priority:** {issue.Priority}");
        sb.AppendLine($"**Type:** {issue.Type}");

        if (!string.IsNullOrWhiteSpace(issue.Body))
        {
            sb.AppendLine();
            sb.AppendLine("## Current Description");
            sb.AppendLine(issue.Body);
        }

        sb.AppendLine();
        sb.AppendLine("Please enhance this issue using the available tools.");
        return sb.ToString().Trim();
    }

    private static IssuePriority ParsePriority(string s) => s switch
    {
        "urgent" => IssuePriority.Urgent,
        "high" => IssuePriority.High,
        "medium" => IssuePriority.Medium,
        "low" => IssuePriority.Low,
        _ => IssuePriority.NoPriority,
    };

    private static IssueType ParseType(string s) => s switch
    {
        "bug" => IssueType.Bug,
        "feature" => IssueType.Feature,
        "task" => IssueType.Task,
        "epic" => IssueType.Epic,
        _ => IssueType.Issue,
    };

    private static IReadOnlyList<object> BuildToolDefinitions() =>
    [
        new
        {
            type = "function",
            function = new
            {
                name = "update_issue_description",
                description = "Update the issue description with enhanced content, implementation details, and acceptance criteria.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        body = new { type = "string", description = "The new Markdown body for the issue." },
                    },
                    required = new[] { "body" },
                },
            },
        },
        new
        {
            type = "function",
            function = new
            {
                name = "create_sub_issue",
                description = "Create a sub-issue under the current issue to track a distinct logical component.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        title = new { type = "string", description = "Sub-issue title." },
                        body = new { type = "string", description = "Sub-issue description (Markdown)." },
                        priority = new
                        {
                            type = "string",
                            @enum = new[] { "no_priority", "urgent", "high", "medium", "low" },
                            description = "Priority level.",
                        },
                        type = new
                        {
                            type = "string",
                            @enum = new[] { "issue", "bug", "feature", "task", "epic" },
                            description = "Issue type.",
                        },
                    },
                    required = new[] { "title" },
                },
            },
        },
        new
        {
            type = "function",
            function = new
            {
                name = "create_task",
                description = "Create a task under the current issue representing a specific implementation step.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        title = new { type = "string", description = "Task title." },
                        body = new { type = "string", description = "Task details (Markdown)." },
                    },
                    required = new[] { "title" },
                },
            },
        },
        new
        {
            type = "function",
            function = new
            {
                name = "list_repo_files",
                description = "List files and directories in the project's git repository to explore the codebase structure.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new
                        {
                            type = "string",
                            description = "Optional directory path to list (e.g. 'src/IssuePit.Api'). Defaults to the root.",
                        },
                        @ref = new
                        {
                            type = "string",
                            description = "Optional branch name or commit SHA. Defaults to the default branch.",
                        },
                    },
                },
            },
        },
        new
        {
            type = "function",
            function = new
            {
                name = "get_repo_file",
                description = "Read the content of a file from the project's git repository to understand codebase context.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        file_path = new
                        {
                            type = "string",
                            description = "Path to the file in the repository (e.g. 'src/Program.cs', 'README.md').",
                        },
                        @ref = new
                        {
                            type = "string",
                            description = "Optional branch name or commit SHA. Defaults to the default branch.",
                        },
                    },
                    required = new[] { "file_path" },
                },
            },
        },
    ];
}
