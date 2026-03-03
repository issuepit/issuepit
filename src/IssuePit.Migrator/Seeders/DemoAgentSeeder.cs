using System.Reflection;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;

namespace IssuePit.Migrator.Seeders;

/// <summary>Seeds demo agents and MCP servers for an organization.</summary>
public class DemoAgentSeeder(IssuePitDbContext db)
{
    public async Task SeedAsync(Guid orgId)
    {
        // --- MCP Servers ---
        var mcpGitHub = new McpServer
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = "GitHub MCP",
            Description = "GitHub API integration — read/write issues, pull requests, branches, and code search.",
            Url = "https://mcp.example.com/github",
            Configuration = "{}",
            AllowedTools = """["create_issue","update_issue","list_issues","search_issues","get_pull_request","list_pull_requests","create_pull_request","search_code","list_commits","get_commit"]""",
            CreatedAt = DateTime.UtcNow,
        };
        var mcpFilesystem = new McpServer
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = "Filesystem MCP",
            Description = "Local filesystem access — read, write, and search files within allowed paths.",
            Url = "https://mcp.example.com/filesystem",
            Configuration = "{}",
            AllowedTools = """["read_file","write_file","list_directory","search_files","create_directory","move_file","get_file_info"]""",
            CreatedAt = DateTime.UtcNow,
        };
        db.McpServers.AddRange(mcpGitHub, mcpFilesystem);
        await db.SaveChangesAsync();

        // --- Agents (system prompts loaded from embedded MD resources) ---
        var planAgent = new Agent
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = "Plan Agent",
            SystemPrompt = LoadSystemPrompt("plan-agent.md"),
            DockerImage = "ghcr.io/sst/opencode:latest",
            AllowedTools = "[]",
            CreatedAt = DateTime.UtcNow,
        };
        var codeAgent = new Agent
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = "Code Agent",
            SystemPrompt = LoadSystemPrompt("code-agent.md"),
            DockerImage = "ghcr.io/sst/opencode:latest",
            AllowedTools = "[]",
            CreatedAt = DateTime.UtcNow,
        };
        var evalAgent = new Agent
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = "Evaluate Agent",
            SystemPrompt = LoadSystemPrompt("evaluate-agent.md"),
            DockerImage = "ghcr.io/sst/opencode:latest",
            AllowedTools = "[]",
            CreatedAt = DateTime.UtcNow,
        };
        var qualityAgent = new Agent
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = "Quality Agent",
            SystemPrompt = LoadSystemPrompt("quality-agent.md"),
            DockerImage = "ghcr.io/sst/opencode:latest",
            AllowedTools = "[]",
            CreatedAt = DateTime.UtcNow,
        };
        db.Agents.AddRange(planAgent, codeAgent, evalAgent, qualityAgent);
        await db.SaveChangesAsync();

        // Link all agents to both MCP servers
        db.AgentMcpServers.AddRange(
            new AgentMcpServer { AgentId = planAgent.Id,    McpServerId = mcpGitHub.Id },
            new AgentMcpServer { AgentId = planAgent.Id,    McpServerId = mcpFilesystem.Id },
            new AgentMcpServer { AgentId = codeAgent.Id,    McpServerId = mcpGitHub.Id },
            new AgentMcpServer { AgentId = codeAgent.Id,    McpServerId = mcpFilesystem.Id },
            new AgentMcpServer { AgentId = evalAgent.Id,    McpServerId = mcpGitHub.Id },
            new AgentMcpServer { AgentId = evalAgent.Id,    McpServerId = mcpFilesystem.Id },
            new AgentMcpServer { AgentId = qualityAgent.Id, McpServerId = mcpGitHub.Id },
            new AgentMcpServer { AgentId = qualityAgent.Id, McpServerId = mcpFilesystem.Id }
        );
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
