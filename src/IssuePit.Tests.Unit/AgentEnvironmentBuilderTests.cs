using System.Text.Json;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.ExecutionClient.Runtimes;
using McpServerEntity = IssuePit.Core.Entities.McpServer;

namespace IssuePit.Tests.Unit;

/// <summary>Unit tests for <see cref="AgentEnvironmentBuilder"/> helper methods.</summary>
[Trait("Category", "Unit")]
public class AgentEnvironmentBuilderTests
{
    private static Agent MakeAgent(
        string name = "my-agent",
        string? model = null,
        string systemPrompt = "Be helpful",
        OpenCodeAgentType? agentType = null,
        IEnumerable<Agent>? childAgents = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            SystemPrompt = systemPrompt,
            DockerImage = "ghcr.io/sst/opencode:latest",
            Model = model,
            AgentType = agentType,
            ChildAgents = childAgents?.ToList() ?? [],
        };

    // ──────────────────────────────────────────────────────────────────────────
    // BuildExtraMcpJson — MCP server serialisation
    // ──────────────────────────────────────────────────────────────────────────

    private static McpServerEntity MakeMcpServer(
        string name,
        string url,
        string configuration = "{}",
        IEnumerable<McpServerSecret>? secrets = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrgId = Guid.NewGuid(),
            Name = name,
            Url = url,
            Configuration = configuration,
            Secrets = secrets?.ToList() ?? [],
        };

    private static McpServerSecret MakeSecret(
        Guid mcpServerId,
        string key,
        string value,
        McpSecretScope scope = McpSecretScope.Global,
        Guid? scopeId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            McpServerId = mcpServerId,
            Key = key,
            EncryptedValue = $"plain:{value}",
            Scope = scope,
            ScopeId = scopeId,
        };

    [Fact]
    public void BuildExtraMcpJson_NoLinkedServers_ReturnsEmpty()
    {
        var agent = MakeAgent();
        var result = AgentEnvironmentBuilder.BuildExtraMcpJson(agent);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildExtraMcpJson_SingleServerNoSecrets_SerializesCorrectly()
    {
        var server = MakeMcpServer("Context7", "https://mcp.context7.com/mcp");
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            Name = "my-agent",
            SystemPrompt = "help",
            DockerImage = "img",
            AgentMcpServers = [new AgentMcpServer { McpServer = server }],
        };

        var json = AgentEnvironmentBuilder.BuildExtraMcpJson(agent);
        var doc = JsonDocument.Parse(json);
        var entry = doc.RootElement[0];

        Assert.Equal("context7", entry.GetProperty("name").GetString());
        Assert.Equal("remote", entry.GetProperty("type").GetString());
        Assert.Equal("https://mcp.context7.com/mcp", entry.GetProperty("url").GetString());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("headers").ValueKind);
    }

    [Fact]
    public void BuildExtraMcpJson_ServerWithGlobalSecret_IncludesHeaderInOutput()
    {
        var serverId = Guid.NewGuid();
        var server = MakeMcpServer(
            "GitHub MCP",
            "https://api.githubcopilot.com/mcp/",
            secrets: [MakeSecret(serverId, "Authorization", "Bearer ghp_TOKEN", McpSecretScope.Global)]);
        server.Id = serverId;

        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            Name = "my-agent",
            SystemPrompt = "help",
            DockerImage = "img",
            AgentMcpServers = [new AgentMcpServer { McpServer = server }],
        };

        var json = AgentEnvironmentBuilder.BuildExtraMcpJson(agent);
        var doc = JsonDocument.Parse(json);
        var entry = doc.RootElement[0];

        Assert.Equal("github-mcp", entry.GetProperty("name").GetString());
        var headers = entry.GetProperty("headers");
        Assert.Equal("Bearer ghp_TOKEN", headers.GetProperty("Authorization").GetString());
    }

    [Fact]
    public void BuildExtraMcpJson_AgentScopedSecretOverridesGlobal()
    {
        var serverId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var server = MakeMcpServer(
            "GitHub MCP",
            "https://api.githubcopilot.com/mcp/",
            secrets:
            [
                MakeSecret(serverId, "Authorization", "Bearer GLOBAL", McpSecretScope.Global),
                MakeSecret(serverId, "Authorization", "Bearer AGENT_SPECIFIC", McpSecretScope.Agent, agentId),
            ]);
        server.Id = serverId;

        var agent = new Agent
        {
            Id = agentId,
            Name = "my-agent",
            SystemPrompt = "help",
            DockerImage = "img",
            AgentMcpServers = [new AgentMcpServer { McpServer = server }],
        };

        var json = AgentEnvironmentBuilder.BuildExtraMcpJson(agent);
        var doc = JsonDocument.Parse(json);
        var headers = doc.RootElement[0].GetProperty("headers");

        // Agent-scoped secret should take precedence over global.
        Assert.Equal("Bearer AGENT_SPECIFIC", headers.GetProperty("Authorization").GetString());
    }

    [Fact]
    public void BuildExtraMcpJson_OtherAgentScopedSecretIsExcluded()
    {
        var serverId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var otherAgentId = Guid.NewGuid();
        var server = MakeMcpServer(
            "GitHub MCP",
            "https://api.githubcopilot.com/mcp/",
            secrets: [MakeSecret(serverId, "Authorization", "Bearer OTHER", McpSecretScope.Agent, otherAgentId)]);
        server.Id = serverId;

        var agent = new Agent
        {
            Id = agentId,
            Name = "my-agent",
            SystemPrompt = "help",
            DockerImage = "img",
            AgentMcpServers = [new AgentMcpServer { McpServer = server }],
        };

        var json = AgentEnvironmentBuilder.BuildExtraMcpJson(agent);
        var doc = JsonDocument.Parse(json);
        var entry = doc.RootElement[0];

        // Secret scoped to a different agent should not be included.
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("headers").ValueKind);
    }

    [Fact]
    public void BuildExtraMcpJson_RemoteTypePassedThrough()
    {
        var server = MakeMcpServer("My Remote Server", "https://example.com/mcp", configuration: """{"type":"remote"}""");
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            Name = "my-agent",
            SystemPrompt = "help",
            DockerImage = "img",
            AgentMcpServers = [new AgentMcpServer { McpServer = server }],
        };

        var json = AgentEnvironmentBuilder.BuildExtraMcpJson(agent);
        var doc = JsonDocument.Parse(json);
        Assert.Equal("remote", doc.RootElement[0].GetProperty("type").GetString());
    }

    [Fact]
    public void BuildExtraMcpJson_LegacySseTypeNormalisedToRemote()
    {
        var server = MakeMcpServer("My SSE Server", "https://example.com/mcp", configuration: """{"type":"sse"}""");
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            Name = "my-agent",
            SystemPrompt = "help",
            DockerImage = "img",
            AgentMcpServers = [new AgentMcpServer { McpServer = server }],
        };

        var json = AgentEnvironmentBuilder.BuildExtraMcpJson(agent);
        var doc = JsonDocument.Parse(json);
        Assert.Equal("remote", doc.RootElement[0].GetProperty("type").GetString());
    }

    [Fact]
    public void BuildExtraMcpJson_LegacyHttpTypeNormalisedToRemote()
    {
        var server = MakeMcpServer("My HTTP Server", "https://example.com/mcp", configuration: """{"type":"http"}""");
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            Name = "my-agent",
            SystemPrompt = "help",
            DockerImage = "img",
            AgentMcpServers = [new AgentMcpServer { McpServer = server }],
        };

        var json = AgentEnvironmentBuilder.BuildExtraMcpJson(agent);
        var doc = JsonDocument.Parse(json);
        Assert.Equal("remote", doc.RootElement[0].GetProperty("type").GetString());
    }

    [Fact]
    public void BuildExtraMcpJson_ServerNameSlugified()
    {
        var server = MakeMcpServer("My MCP Server", "https://example.com/mcp");
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            Name = "my-agent",
            SystemPrompt = "help",
            DockerImage = "img",
            AgentMcpServers = [new AgentMcpServer { McpServer = server }],
        };

        var json = AgentEnvironmentBuilder.BuildExtraMcpJson(agent);
        var doc = JsonDocument.Parse(json);
        Assert.Equal("my-mcp-server", doc.RootElement[0].GetProperty("name").GetString());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // BuildAgentsJson — agent type serialisation
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildAgentsJson_NoAgentType_OmitsAgentTypeFromJson()
    {
        var agent = MakeAgent(agentType: null);
        var json = AgentEnvironmentBuilder.BuildAgentsJson(agent);
        var doc = JsonDocument.Parse(json);
        var entry = doc.RootElement[0];
        // agentType should be null (not absent, but serialised as null)
        Assert.True(entry.TryGetProperty("agentType", out var prop));
        Assert.Equal(JsonValueKind.Null, prop.ValueKind);
    }

    [Fact]
    public void BuildAgentsJson_PrimaryAgentType_SerialisesPrimary()
    {
        var agent = MakeAgent(agentType: OpenCodeAgentType.Primary);
        var json = AgentEnvironmentBuilder.BuildAgentsJson(agent);
        var doc = JsonDocument.Parse(json);
        var entry = doc.RootElement[0];
        Assert.Equal("primary", entry.GetProperty("agentType").GetString());
    }

    [Fact]
    public void BuildAgentsJson_SubAgentType_SerialisesSubagent()
    {
        var agent = MakeAgent(agentType: OpenCodeAgentType.SubAgent);
        var json = AgentEnvironmentBuilder.BuildAgentsJson(agent);
        var doc = JsonDocument.Parse(json);
        var entry = doc.RootElement[0];
        Assert.Equal("subagent", entry.GetProperty("agentType").GetString());
    }

    [Fact]
    public void BuildAgentsJson_AllAgentType_SerialisesAll()
    {
        var agent = MakeAgent(agentType: OpenCodeAgentType.All);
        var json = AgentEnvironmentBuilder.BuildAgentsJson(agent);
        var doc = JsonDocument.Parse(json);
        var entry = doc.RootElement[0];
        Assert.Equal("all", entry.GetProperty("agentType").GetString());
    }

    [Fact]
    public void BuildAgentsJson_IncludesChildAgents()
    {
        var child = MakeAgent(name: "sub-agent", agentType: OpenCodeAgentType.SubAgent);
        var parent = MakeAgent(name: "primary-agent", agentType: OpenCodeAgentType.Primary, childAgents: [child]);
        var json = AgentEnvironmentBuilder.BuildAgentsJson(parent);
        var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;
        Assert.Equal(2, arr.GetArrayLength());
        Assert.Equal("primary-agent", arr[0].GetProperty("name").GetString());
        Assert.Equal("primary", arr[0].GetProperty("agentType").GetString());
        Assert.Equal("sub-agent", arr[1].GetProperty("name").GetString());
        Assert.Equal("subagent", arr[1].GetProperty("agentType").GetString());
    }

    [Fact]
    public void BuildAgentsJson_ChildAgentWithoutType_SerialisesNull()
    {
        var child = MakeAgent(name: "untyped-child", agentType: null);
        var parent = MakeAgent(name: "parent", childAgents: [child]);
        var json = AgentEnvironmentBuilder.BuildAgentsJson(parent);
        var doc = JsonDocument.Parse(json);
        var childEntry = doc.RootElement[1];
        Assert.Equal(JsonValueKind.Null, childEntry.GetProperty("agentType").ValueKind);
    }

    [Fact]
    public void BuildAgentsJson_AgentWithModel_IncludesModel()
    {
        var agent = MakeAgent(model: "anthropic/claude-sonnet-4-5", agentType: OpenCodeAgentType.Primary);
        var json = AgentEnvironmentBuilder.BuildAgentsJson(agent);
        var doc = JsonDocument.Parse(json);
        Assert.Equal("anthropic/claude-sonnet-4-5", doc.RootElement[0].GetProperty("model").GetString());
    }

    [Fact]
    public void BuildAgentsJson_AlwaysIncludesNameAndPrompt()
    {
        var agent = MakeAgent(name: "review-agent", systemPrompt: "Review code carefully.");
        var json = AgentEnvironmentBuilder.BuildAgentsJson(agent);
        var doc = JsonDocument.Parse(json);
        var entry = doc.RootElement[0];
        Assert.Equal("review-agent", entry.GetProperty("name").GetString());
        Assert.Equal("Review code carefully.", entry.GetProperty("prompt").GetString());
    }
}
