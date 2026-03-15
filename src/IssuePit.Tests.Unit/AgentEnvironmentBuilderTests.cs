using System.Text.Json;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.ExecutionClient.Runtimes;

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
