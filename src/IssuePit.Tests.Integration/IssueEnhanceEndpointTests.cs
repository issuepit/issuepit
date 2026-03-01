using System.Net;
using System.Net.Http.Json;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class IssueEnhanceEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid projectId, Guid orgId)> SeedProjectAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"host-{tenantId}" });

        var org = new Organization { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Org", Slug = $"org-{tenantId}" };
        db.Organizations.Add(org);

        var project = new Project { Id = Guid.NewGuid(), OrgId = org.Id, Name = "Proj", Slug = $"proj-{tenantId}" };
        db.Projects.Add(project);

        await db.SaveChangesAsync();
        return (tenantId, project.Id, org.Id);
    }

    [Fact]
    public async Task EnhanceIssue_WhenNoTenant_Returns401()
    {
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");

        var response = await _client.PostAsync($"/api/issues/{Guid.NewGuid()}/enhance", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EnhanceIssue_WhenIssueNotFound_Returns404()
    {
        var (tenantId, _, _) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsync($"/api/issues/{Guid.NewGuid()}/enhance", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task EnhanceIssue_WhenNoOpenRouterKey_Returns400WithError()
    {
        var (tenantId, projectId, _) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Test Issue", Number = 1 };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsync($"/api/issues/{issue.Id}/enhance", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var error = body.GetProperty("error").GetString();
        Assert.Contains("OpenRouter", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("API key", error, StringComparison.OrdinalIgnoreCase);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task ApiKeyResolver_ProjectScopedKey_TakesPriorityOverOrgKey()
    {
        var (_, projectId, orgId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        // Seed an org-level key and a project-level key
        db.ApiKeys.Add(new ApiKey
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = "Org Key",
            Provider = ApiKeyProvider.OpenRouter,
            EncryptedValue = "plain:org-key",
        });
        var projectKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            ProjectId = projectId,
            Name = "Project Key",
            Provider = ApiKeyProvider.OpenRouter,
            EncryptedValue = "plain:project-key",
        };
        db.ApiKeys.Add(projectKey);
        await db.SaveChangesAsync();

        var resolver = scope.ServiceProvider.GetRequiredService<ApiKeyResolverService>();
        var resolved = await resolver.ResolveAsync(orgId, ApiKeyProvider.OpenRouter, projectId: projectId);

        Assert.NotNull(resolved);
        Assert.Equal(projectKey.Id, resolved.Id);
        Assert.Equal("project-key", ApiKeyResolverService.DecryptValue(resolved.EncryptedValue));
    }
}
