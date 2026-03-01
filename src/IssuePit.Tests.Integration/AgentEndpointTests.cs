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

    private async Task<(Guid tenantId, Guid orgId)> SeedTenantAndOrgAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Hostname = $"test-{tenantId}" });
        db.Organizations.Add(new Organization { Id = orgId, TenantId = tenantId, Name = "Org", Slug = $"org-{orgId}" });
        await db.SaveChangesAsync();
        return (tenantId, orgId);
    }

    [Fact]
    public async Task GetMcpTemplates_Returns200WithTemplates()
    {
        var response = await _client.GetAsync("/api/mcp-servers/templates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<McpTemplateDto>>();
        Assert.NotNull(body);
        Assert.NotEmpty(body);
        Assert.Contains(body, t => t.Key == "github");
        Assert.Contains(body, t => t.Key == "playwright");
    }

    [Fact]
    public async Task GetMcpServers_WithoutTenant_Returns401()
    {
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        var response = await _client.GetAsync("/api/mcp-servers");
        // When no valid tenant can be resolved, the endpoint returns 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndGetMcpServer_RoundTrip_Succeeds()
    {
        var (tenantId, orgId) = await SeedTenantAndOrgAsync();
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var payload = new
        {
            OrgId = orgId,
            Name = "Test GitHub MCP",
            Description = "For integration tests",
            Url = "npx @modelcontextprotocol/server-github",
            AllowedTools = """["create_issue","list_issues"]""",
            Configuration = """{"env":{"GITHUB_PERSONAL_ACCESS_TOKEN":"test"}}""",
        };

        var createResponse = await _client.PostAsJsonAsync("/api/mcp-servers", payload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<McpServerDto>();
        Assert.NotNull(created);
        Assert.Equal("Test GitHub MCP", created.Name);
        Assert.Equal("For integration tests", created.Description);

        // GET by ID
        var getResponse = await _client.GetAsync($"/api/mcp-servers/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task UpdateMcpServer_Succeeds()
    {
        var (tenantId, orgId) = await SeedTenantAndOrgAsync();
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var createPayload = new
        {
            OrgId = orgId,
            Name = "Original Name",
            Description = (string?)null,
            Url = "npx some-mcp",
            AllowedTools = "[]",
            Configuration = "{}",
        };
        var created = await (await _client.PostAsJsonAsync("/api/mcp-servers", createPayload))
            .Content.ReadFromJsonAsync<McpServerDto>();
        Assert.NotNull(created);

        var updatePayload = new
        {
            OrgId = orgId,
            Name = "Updated Name",
            Description = "Updated desc",
            Url = "npx updated-mcp",
            AllowedTools = """["tool_a"]""",
            Configuration = """{"key":"value"}""",
        };
        var putResponse = await _client.PutAsJsonAsync($"/api/mcp-servers/{created.Id}", updatePayload);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var updated = await putResponse.Content.ReadFromJsonAsync<McpServerDto>();
        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal("Updated desc", updated.Description);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task DeleteMcpServer_Succeeds()
    {
        var (tenantId, orgId) = await SeedTenantAndOrgAsync();
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var createPayload = new
        {
            OrgId = orgId,
            Name = "To Delete",
            Description = (string?)null,
            Url = "npx mcp-to-delete",
            AllowedTools = "[]",
            Configuration = "{}",
        };
        var created = await (await _client.PostAsJsonAsync("/api/mcp-servers", createPayload))
            .Content.ReadFromJsonAsync<McpServerDto>();
        Assert.NotNull(created);

        var deleteResponse = await _client.DeleteAsync($"/api/mcp-servers/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/mcp-servers/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task LinkAndUnlinkAgentMcpServer_Succeeds()
    {
        var (tenantId, orgId) = await SeedTenantAndOrgAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = "Link Test Agent",
            SystemPrompt = "test",
            DockerImage = "ubuntu:24.04",
        };
        var mcpServer = new McpServer
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = "Link Test MCP",
            Url = "npx test-mcp",
        };
        db.Agents.Add(agent);
        db.McpServers.Add(mcpServer);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Link
        var linkResponse = await _client.PostAsJsonAsync(
            $"/api/agents/{agent.Id}/mcp-servers/{mcpServer.Id}", new { });
        Assert.Equal(HttpStatusCode.Created, linkResponse.StatusCode);

        // Linking again returns conflict
        var conflictResponse = await _client.PostAsJsonAsync(
            $"/api/agents/{agent.Id}/mcp-servers/{mcpServer.Id}", new { });
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        // Unlink
        var unlinkResponse = await _client.DeleteAsync(
            $"/api/agents/{agent.Id}/mcp-servers/{mcpServer.Id}");
        Assert.Equal(HttpStatusCode.NoContent, unlinkResponse.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    // DTOs for deserializing responses
    private record McpServerDto(Guid Id, string Name, string? Description, string Url, string AllowedTools, string Configuration);
    private record McpTemplateDto(string Key, string Name, string Description, string Url);
}
