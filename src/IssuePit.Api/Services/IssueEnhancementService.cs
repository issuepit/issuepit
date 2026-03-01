using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace IssuePit.Api.Services;

/// <summary>
/// Enhances an issue using an LLM via OpenRouter by extending its description,
/// creating sub-issues, and creating tasks. Tool calls are routed through the
/// existing MCP server (running in enhance mode) rather than re-implementing
/// the tool logic here — giving the LLM access to all MCP tools including label
/// editing, issue updates, task management, and repository file access.
/// </summary>
public class IssueEnhancementService(
    IssuePitDbContext db,
    GitService gitService,
    ApiKeyResolverService keyResolver,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory,
    ILogger<IssueEnhancementService> logger)
{
    private const string OpenRouterBaseUrl = "https://openrouter.ai/api/v1";
    private const string DefaultModel = "anthropic/claude-3.5-sonnet";

    // Tools exposed by the MCP server that are relevant for issue enhancement.
    // This set mirrors what the MCP server exposes in EnhanceMode (issue, task, and
    // repository file tools). Any tool added to the MCP server that belongs to these
    // categories will automatically become available here via ListToolsAsync().
    private static readonly IReadOnlySet<string> EnhancementToolAllowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // IssueTools
        "ListIssues", "GetIssue", "ListSubIssues", "CreateIssue", "UpdateIssue",
        // TaskTools
        "ListIssueTasks", "CreateIssueTask", "UpdateIssueTask",
        // RepoFileTools
        "ListRepoFiles", "GetRepoFile",
    };

    // Tools that require an issueId argument injected automatically.
    // Maintained as an explicit set rather than relying on string matching
    // to stay robust when tool names change.
    private static readonly IReadOnlySet<string> ToolsRequiringIssueId = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ListIssueTasks", "CreateIssueTask", "UpdateIssueTask",
    };

    private const string TenantIdHeader = "X-Tenant-Id";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EnhanceAsync(Guid issueId, CancellationToken ct = default)
    {
        var issue = await db.Issues
            .Include(i => i.Project)
            .ThenInclude(p => p!.Organization)
            .ThenInclude(o => o!.Tenant)
            .FirstOrDefaultAsync(i => i.Id == issueId, ct)
            ?? throw new InvalidOperationException($"Issue {issueId} not found.");

        var orgId = issue.Project!.OrgId;
        var tenantId = issue.Project.Organization.TenantId;

        // Resolve the most specific OpenRouter key: project → team → user → org
        var apiKey = await keyResolver.ResolveAsync(
            orgId, ApiKeyProvider.OpenRouter, projectId: issue.ProjectId, ct: ct)
            ?? throw new InvalidOperationException(
                "No OpenRouter API key configured for this organization. " +
                "Add one via POST /api/config/keys with provider 'OpenRouter'.");

        var plainKey = ApiKeyResolverService.DecryptValue(apiKey.EncryptedValue);

        // Inject agents.md content from the git repository if available
        var agentsMd = await ReadAgentsMdAsync(issue.ProjectId, ct);

        // Build MCP client connected to the running MCP server.
        // The MCP server handles all tool execution (issue/task/repo operations) so that
        // this service does not need to duplicate any business logic.
        await using var mcpClient = await CreateMcpClientAsync(tenantId, ct);

        // Fetch tool definitions from the MCP server and filter to the enhance-mode allow list.
        var allMcpTools = await mcpClient.ListToolsAsync(cancellationToken: ct);
        var enhanceTools = allMcpTools
            .Where(t => EnhancementToolAllowList.Contains(t.Name))
            .ToList();

        var toolDefinitions = BuildOpenRouterToolDefinitions(enhanceTools);

        var systemPrompt = BuildEnhanceSystemPrompt(agentsMd, issue.ProjectId);
        var userMessage = BuildIssueUserMessage(issue);

        await RunLlmLoopAsync(mcpClient, issue, plainKey, systemPrompt, userMessage, toolDefinitions, ct);
    }

    private async Task<McpClient> CreateMcpClientAsync(Guid tenantId, CancellationToken ct)
    {
        // Create an HttpClient that forwards the tenant ID header to the MCP server.
        var httpClient = httpClientFactory.CreateClient("mcp-server");
        httpClient.DefaultRequestHeaders.Remove(TenantIdHeader);
        httpClient.DefaultRequestHeaders.Add(TenantIdHeader, tenantId.ToString());

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = httpClient.BaseAddress! },
            httpClient,
            loggerFactory,
            ownsHttpClient: false);

        return await McpClient.CreateAsync(transport, loggerFactory: loggerFactory, cancellationToken: ct);
    }

    private async Task RunLlmLoopAsync(
        McpClient mcpClient,
        Core.Entities.Issue issue,
        string apiKey,
        string systemPrompt,
        string userMessage,
        IReadOnlyList<object> tools,
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
            var requestBody = new { model = DefaultModel, messages, tools };
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

                var result = await ExecuteToolViaMcpAsync(mcpClient, functionName, argumentsJson, issue, ct);
                messages.Add(new { role = "tool", tool_call_id = callId, content = result });
            }
        }
    }

    /// <summary>
    /// Proxies a tool call to the MCP server and returns the text result.
    /// All business logic (DB writes, git access, label updates, etc.) is handled by
    /// the MCP server tools — this service only orchestrates the LLM loop.
    /// </summary>
    private async Task<string> ExecuteToolViaMcpAsync(
        McpClient mcpClient,
        string toolName,
        string argumentsJson,
        Core.Entities.Issue issue,
        CancellationToken ct)
    {
        try
        {
            // Deserialize LLM arguments as key→value pairs for the MCP call.
            var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson, JsonOptions)
                ?? [];

            // Inject the project ID for tools that need it (issue and repo file tools).
            // The LLM doesn't know the project ID so we supply it transparently.
            if (!args.ContainsKey("projectId"))
                args["projectId"] = issue.ProjectId;
            if (!args.ContainsKey("issueId") && ToolsRequiringIssueId.Contains(toolName))
                args["issueId"] = issue.Id;

            var callResult = await mcpClient.CallToolAsync(toolName, args, cancellationToken: ct);

            // Extract text from the result content blocks
            var text = string.Join("\n", callResult.Content.OfType<TextContentBlock>().Select(c => c.Text));
            return string.IsNullOrEmpty(text) ? "Tool executed successfully." : text;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP tool call '{Tool}' failed for issue {IssueId}.", toolName, issue.Id);
            return $"Error executing {toolName}: {ex.Message}";
        }
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

    /// <summary>
    /// Converts MCP tool definitions (fetched from the MCP server) into the
    /// OpenRouter/OpenAI function-calling format.
    /// </summary>
    private static IReadOnlyList<object> BuildOpenRouterToolDefinitions(IEnumerable<McpClientTool> tools) =>
        tools.Select(t => (object)new
        {
            type = "function",
            function = new
            {
                t.ProtocolTool.Name,
                description = t.Description,
                parameters = t.ProtocolTool.InputSchema,
            },
        }).ToList();

    private static string BuildEnhanceSystemPrompt(string? agentsMd, Guid projectId)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"""
            You are an AI assistant specializing in software project management and technical planning.
            You are enhancing issues in project {projectId}.

            Your task is to enhance a given software issue by:

            1. Extending the description with implementation details, context, technical guidance, and clear acceptance criteria.
               Use UpdateIssue to persist the updated description.
            2. Breaking the issue into specific, independently-trackable sub-issues for each logical component.
               Use CreateIssue with the current issue's ID as parentIssueId to create sub-issues.
            3. Creating concrete tasks that represent the implementation steps.
               Use CreateIssueTask to add tasks to the issue.

            Use the available tools to persist your changes. Do not ask for confirmation — proceed directly.

            Guidelines:
            - Sub-issues should represent distinct pieces of work that can be tracked separately.
            - Tasks should be specific, actionable implementation steps (e.g. "Add API endpoint", "Write unit tests").
            - Enhanced descriptions should include: context, acceptance criteria, and technical considerations.
            - Use ListRepoFiles to explore the repository structure and GetRepoFile to read relevant source files for context.
            """);

        if (!string.IsNullOrWhiteSpace(agentsMd))
        {
            sb.AppendLine();
            sb.AppendLine("## Project Agent Guidelines (agents.md)");
            sb.AppendLine(agentsMd);
        }

        return sb.ToString().Trim();
    }

    private static string BuildIssueUserMessage(Core.Entities.Issue issue)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Issue #{issue.Number} (id: {issue.Id}): {issue.Title}");
        sb.AppendLine();
        sb.AppendLine($"**Project:** {issue.ProjectId}");
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
}
