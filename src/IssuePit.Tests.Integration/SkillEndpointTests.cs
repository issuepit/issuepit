using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class SkillEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private async Task<Guid> SeedSkillAsync(Guid orgId, string name = "Test Skill")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var skill = new Skill
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = name,
            Content = "You are a test skill.",
            SyncStatus = SkillSyncStatus.None,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Skills.Add(skill);
        await db.SaveChangesAsync();
        return skill.Id;
    }

    private async Task<Guid> SeedAgentAsync(Guid orgId)
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
            CreatedAt = DateTime.UtcNow,
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();
        return agent.Id;
    }

    private async Task<Guid> SeedProjectAsync(Guid orgId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = "Test Project",
            Slug = $"test-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    [Fact]
    public async Task GetSkills_WithTenantHeader_Returns200()
    {
        var (tenantId, orgId) = await SeedOrgAsync();
        await SeedSkillAsync(orgId);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync("/api/skills");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetSkill_WithValidId_Returns200()
    {
        var (tenantId, orgId) = await SeedOrgAsync();
        var skillId = await SeedSkillAsync(orgId);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/skills/{skillId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task CreateSkill_WithValidPayload_Returns201()
    {
        var (tenantId, orgId) = await SeedOrgAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var payload = new
        {
            orgId,
            name = "New Skill",
            description = "A new skill for testing",
            content = "You are a Python expert.",
            gitRepoUrl = (string?)null,
            gitSubDir = (string?)null,
            gitBranch = (string?)null,
            gitSha = (string?)null,
            gitAuthUsername = (string?)null,
            gitAuthToken = (string?)null,
        };

        var response = await _client.PostAsJsonAsync("/api/skills", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task CreateSkill_WithGitBranchAndSha_Returns201WithFields()
    {
        var (tenantId, orgId) = await SeedOrgAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var payload = new
        {
            orgId,
            name = "Pinned Skill",
            description = "Skill pinned to a branch and SHA",
            content = "You are a pinned skill.",
            gitRepoUrl = "https://github.com/example/skills.git",
            gitSubDir = (string?)null,
            gitBranch = "main",
            gitSha = "abc1234def",
            gitAuthUsername = (string?)null,
            gitAuthToken = (string?)null,
        };

        var response = await _client.PostAsJsonAsync("/api/skills", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task UpdateSkill_WithValidPayload_Returns200()
    {
        var (tenantId, orgId) = await SeedOrgAsync();
        var skillId = await SeedSkillAsync(orgId);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var payload = new
        {
            name = "Updated Skill",
            description = "Updated description",
            content = "You are an updated test skill.",
            gitRepoUrl = (string?)null,
            gitSubDir = (string?)null,
            gitBranch = (string?)null,
            gitSha = (string?)null,
            gitAuthUsername = (string?)null,
            gitAuthToken = (string?)null,
        };

        var response = await _client.PutAsJsonAsync($"/api/skills/{skillId}", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<SkillDto>();
        Assert.Equal("Updated Skill", updated?.Name);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task DeleteSkill_WithValidId_Returns204()
    {
        var (tenantId, orgId) = await SeedOrgAsync();
        var skillId = await SeedSkillAsync(orgId, "Skill To Delete");

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.DeleteAsync($"/api/skills/{skillId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task LinkSkill_ToAgent_Returns201()
    {
        var (tenantId, orgId) = await SeedOrgAsync();
        var skillId = await SeedSkillAsync(orgId);
        var agentId = await SeedAgentAsync(orgId);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync($"/api/skills/{skillId}/agents/{agentId}", new { });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task UnlinkSkill_FromAgent_Returns204()
    {
        var (tenantId, orgId) = await SeedOrgAsync();
        var skillId = await SeedSkillAsync(orgId);
        var agentId = await SeedAgentAsync(orgId);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        await _client.PostAsJsonAsync($"/api/skills/{skillId}/agents/{agentId}", new { });
        var response = await _client.DeleteAsync($"/api/skills/{skillId}/agents/{agentId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task LinkSkill_ToProject_Returns201()
    {
        var (tenantId, orgId) = await SeedOrgAsync();
        var skillId = await SeedSkillAsync(orgId);
        var projectId = await SeedProjectAsync(orgId);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync($"/api/skills/{skillId}/projects/{projectId}", new { });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task UnlinkSkill_FromProject_Returns204()
    {
        var (tenantId, orgId) = await SeedOrgAsync();
        var skillId = await SeedSkillAsync(orgId);
        var projectId = await SeedProjectAsync(orgId);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        await _client.PostAsJsonAsync($"/api/skills/{skillId}/projects/{projectId}", new { });
        var response = await _client.DeleteAsync($"/api/skills/{skillId}/projects/{projectId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetProjectSkills_Returns200()
    {
        var (tenantId, orgId) = await SeedOrgAsync();
        var projectId = await SeedProjectAsync(orgId);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/projects/{projectId}/skills");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    private sealed record SkillDto(string Name);
}
