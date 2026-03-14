using System.Reflection;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.Migrator.Seeders;

/// <summary>Seeds demo agents and MCP servers for an organization.</summary>
public class DemoAgentSeeder(IssuePitDbContext db)
{
    public async Task SeedAsync(Guid orgId)
    {
        // --- MCP Servers ---
        var (mcpGitHub, _) = await db.McpServers.AddIfNotExistsAsync(
            s => s.OrgId == orgId && s.Name == "GitHub MCP",
            new McpServer
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                Name = "GitHub MCP",
                Description = "GitHub API integration — read/write issues, pull requests, branches, and code search.",
                Url = "https://mcp.example.com/github",
                Configuration = "{}",
                AllowedTools = """["create_issue","update_issue","list_issues","search_issues","get_pull_request","list_pull_requests","create_pull_request","search_code","list_commits","get_commit"]""",
                CreatedAt = DateTime.UtcNow,
            });
        var (mcpFilesystem, _) = await db.McpServers.AddIfNotExistsAsync(
            s => s.OrgId == orgId && s.Name == "Filesystem MCP",
            new McpServer
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                Name = "Filesystem MCP",
                Description = "Local filesystem access — read, write, and search files within allowed paths.",
                Url = "https://mcp.example.com/filesystem",
                Configuration = "{}",
                AllowedTools = """["read_file","write_file","list_directory","search_files","create_directory","move_file","get_file_info"]""",
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
                DockerImage = "ghcr.io/issuepit/issuepit-helper-opencode-act:main-dotnet10-node24",
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
                DockerImage = "ghcr.io/issuepit/issuepit-helper-opencode-act:main-dotnet10-node24",
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
                DockerImage = "ghcr.io/issuepit/issuepit-helper-opencode-act:main-dotnet10-node24",
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
                DockerImage = "ghcr.io/issuepit/issuepit-helper-opencode-act:main-dotnet10-node24",
                RunnerType = RunnerType.OpenCode,
                IsActive = true,
                AllowedTools = "[]",
                CreatedAt = DateTime.UtcNow,
            });
        qualityAgent.RunnerType ??= RunnerType.OpenCode;
        qualityAgent.IsActive = true;
        await db.SaveChangesAsync();

        // Link all agents to both MCP servers
        await db.AgentMcpServers.AddIfNotExistsAsync(l => l.AgentId == planAgent.Id    && l.McpServerId == mcpGitHub.Id,     new AgentMcpServer { AgentId = planAgent.Id,    McpServerId = mcpGitHub.Id });
        await db.AgentMcpServers.AddIfNotExistsAsync(l => l.AgentId == planAgent.Id    && l.McpServerId == mcpFilesystem.Id, new AgentMcpServer { AgentId = planAgent.Id,    McpServerId = mcpFilesystem.Id });
        await db.AgentMcpServers.AddIfNotExistsAsync(l => l.AgentId == codeAgent.Id    && l.McpServerId == mcpGitHub.Id,     new AgentMcpServer { AgentId = codeAgent.Id,    McpServerId = mcpGitHub.Id });
        await db.AgentMcpServers.AddIfNotExistsAsync(l => l.AgentId == codeAgent.Id    && l.McpServerId == mcpFilesystem.Id, new AgentMcpServer { AgentId = codeAgent.Id,    McpServerId = mcpFilesystem.Id });
        await db.AgentMcpServers.AddIfNotExistsAsync(l => l.AgentId == evalAgent.Id    && l.McpServerId == mcpGitHub.Id,     new AgentMcpServer { AgentId = evalAgent.Id,    McpServerId = mcpGitHub.Id });
        await db.AgentMcpServers.AddIfNotExistsAsync(l => l.AgentId == evalAgent.Id    && l.McpServerId == mcpFilesystem.Id, new AgentMcpServer { AgentId = evalAgent.Id,    McpServerId = mcpFilesystem.Id });
        await db.AgentMcpServers.AddIfNotExistsAsync(l => l.AgentId == qualityAgent.Id && l.McpServerId == mcpGitHub.Id,     new AgentMcpServer { AgentId = qualityAgent.Id, McpServerId = mcpGitHub.Id });
        await db.AgentMcpServers.AddIfNotExistsAsync(l => l.AgentId == qualityAgent.Id && l.McpServerId == mcpFilesystem.Id, new AgentMcpServer { AgentId = qualityAgent.Id, McpServerId = mcpFilesystem.Id });
        await db.SaveChangesAsync();
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
