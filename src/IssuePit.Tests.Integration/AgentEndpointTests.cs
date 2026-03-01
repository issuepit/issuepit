using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class AgentEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid orgId)> SeedOrgAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"host-{tenantId}" });
        var org = new Organization { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Org", Slug = $"org-{tenantId}" };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        return (tenantId, org.Id);
    }

    private async Task<Guid> SeedAgentAsync(Guid orgId, bool isActive = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = "Test Agent",
            SystemPrompt = "You are a test agent.",
            DockerImage = "ghcr.io/test/agent:latest",
            AllowedTools = "[]",
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();
        return agent.Id;
    }

    [Fact]
    public async Task GetAgents_WithTenantHeader_Returns200()
    {
        var (tenantId, orgId) = await SeedOrgAsync();
        await SeedAgentAsync(orgId);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync("/api/agents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task ToggleAgent_Activate_Returns200WithIsActiveTrue()
    {
        var (tenantId, orgId) = await SeedOrgAsync();
        var agentId = await SeedAgentAsync(orgId);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PatchAsJsonAsync($"/api/agents/{agentId}/active", new { isActive = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<AgentDto>();
        Assert.True(updated?.IsActive);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task ToggleAgent_Deactivate_Returns200WithIsActiveFalse()
    {
        var (tenantId, orgId) = await SeedOrgAsync();
        var agentId = await SeedAgentAsync(orgId, isActive: true);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PatchAsJsonAsync($"/api/agents/{agentId}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<AgentDto>();
        Assert.False(updated?.IsActive);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    private sealed record AgentDto(bool IsActive);
}
