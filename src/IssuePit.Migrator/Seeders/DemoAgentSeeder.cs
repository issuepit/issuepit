using System.Reflection;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.Migrator.Seeders;

/// <summary>Seeds demo agents and MCP servers for an organization.</summary>
public class DemoAgentSeeder(IssuePitDbContext db)
{
    public async Task<(Agent PlanAgent, Agent CodeAgent, Agent EvalAgent, Agent QualityAgent)> SeedAsync(Guid orgId)
    {
        // --- MCP Servers ---
        // These are demo/template entries. GitHub MCP and Filesystem MCP use placeholder URLs.
        // Context7 and Fetch MCP are pre-configured with their known public endpoints.
        // They are not linked to agents by default — use the IssuePit MCP server instead
        // (injected automatically via ISSUEPIT_MCP_URL at agent run time).
        await db.McpServers.AddIfNotExistsAsync(
            s => s.OrgId == orgId && s.Name == "GitHub MCP",
            new McpServer
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                Name = "GitHub MCP",
                Description = "GitHub API integration — read/write issues, pull requests, branches, and code search.",
                ServerType = McpServerType.Remote,
                Url = "https://api.githubcopilot.com/mcp/",
                Configuration = """{"type":"remote"}""",
                AllowedTools = """["create_issue","update_issue","list_issues","search_issues","get_pull_request","list_pull_requests","create_pull_request","search_code","list_commits","get_commit"]""",
                CreatedAt = DateTime.UtcNow,
            });
        await db.McpServers.AddIfNotExistsAsync(
            s => s.OrgId == orgId && s.Name == "Filesystem MCP",
            new McpServer
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                Name = "Filesystem MCP",
                Description = "Local filesystem access — read, write, and search files within allowed paths.",
                ServerType = McpServerType.Local,
                Url = "",
                Configuration = """{"command":"npx","args":["-y","@modelcontextprotocol/server-filesystem","/workspace"],"env":{}}""",
                AllowedTools = """["read_file","write_file","list_directory","search_files","create_directory","move_file","get_file_info"]""",
                CreatedAt = DateTime.UtcNow,
            });
        await db.McpServers.AddIfNotExistsAsync(
            s => s.OrgId == orgId && s.Name == "Fetch MCP",
            new McpServer
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                Name = "Fetch MCP",
                Description = "HTTP fetch tools — retrieve web pages, APIs, or raw URLs as markdown or plain text.",
                ServerType = McpServerType.Local,
                Url = "",
                Configuration = """{"command":"uvx","args":["mcp-server-fetch"],"env":{}}""",
                AllowedTools = "[]",
                CreatedAt = DateTime.UtcNow,
            });
        await db.McpServers.AddIfNotExistsAsync(
            s => s.OrgId == orgId && s.Name == "Context7",
            new McpServer
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                Name = "Context7",
                Description = "Context7 — up-to-date library documentation and code examples for popular packages.",
                ServerType = McpServerType.Remote,
                Url = "https://mcp.context7.com/mcp",
                Configuration = """{"type":"remote"}""",
                AllowedTools = "[]",
                CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        // --- Agents (system prompts loaded from embedded MD resources) ---
        var (planAgent, _) = await db.Agents.AddIfNotExistsAsync(
            a => a.OrgId == orgId && a.Name == "Plan Agent",
            new Agent
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                Name = "Plan Agent",
                SystemPrompt = LoadSystemPrompt("plan-agent.md"),
                RunnerType = RunnerType.OpenCode,
                IsActive = true,
                AllowedTools = "[]",
                CreatedAt = DateTime.UtcNow,
            });
        // Ensure existing agents also have the runner type set
        planAgent.RunnerType ??= RunnerType.OpenCode;
        planAgent.IsActive = true;

        var (codeAgent, _) = await db.Agents.AddIfNotExistsAsync(
            a => a.OrgId == orgId && a.Name == "Code Agent",
            new Agent
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                Name = "Code Agent",
                SystemPrompt = LoadSystemPrompt("code-agent.md"),
                RunnerType = RunnerType.OpenCode,
                IsActive = true,
                AllowedTools = "[]",
                CreatedAt = DateTime.UtcNow,
            });
        codeAgent.RunnerType ??= RunnerType.OpenCode;
        codeAgent.IsActive = true;

        var (evalAgent, _) = await db.Agents.AddIfNotExistsAsync(
            a => a.OrgId == orgId && a.Name == "Evaluate Agent",
            new Agent
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                Name = "Evaluate Agent",
                SystemPrompt = LoadSystemPrompt("evaluate-agent.md"),
                RunnerType = RunnerType.OpenCode,
                IsActive = true,
                AllowedTools = "[]",
                CreatedAt = DateTime.UtcNow,
            });
        evalAgent.RunnerType ??= RunnerType.OpenCode;
        evalAgent.IsActive = true;

        var (qualityAgent, _) = await db.Agents.AddIfNotExistsAsync(
            a => a.OrgId == orgId && a.Name == "Quality Agent",
            new Agent
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                Name = "Quality Agent",
                SystemPrompt = LoadSystemPrompt("quality-agent.md"),
                RunnerType = RunnerType.OpenCode,
                IsActive = true,
                AllowedTools = "[]",
                CreatedAt = DateTime.UtcNow,
            });
        qualityAgent.RunnerType ??= RunnerType.OpenCode;
        qualityAgent.IsActive = true;
        await db.SaveChangesAsync();

        // Note: MCP server links to the placeholder GitHub/Filesystem servers have been removed.
        // The IssuePit MCP server is injected automatically via ISSUEPIT_MCP_URL at agent run time.

        return (planAgent, codeAgent, evalAgent, qualityAgent);
    }

    private static string LoadSystemPrompt(string fileName)
    {
        var assembly = typeof(DemoAgentSeeder).Assembly;
        var resourceName = $"IssuePit.Migrator.Seeders.SystemPrompts.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
