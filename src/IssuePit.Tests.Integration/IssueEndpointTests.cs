using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class IssueEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid projectId)> SeedProjectAsync()
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
        return (tenantId, project.Id);
    }

    [Fact]
    public async Task CreateIssue_WithProjectIdOnly_Returns201()
    {
        var (tenantId, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync("/api/issues",
            new { title = "Test Issue", projectId, status = IssueStatus.Backlog, priority = IssuePriority.NoPriority, type = IssueType.Issue });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    /// <summary>
    /// Reproduces the kanban lane bug: issue creation was failing with 400 "The Project field is required"
    /// because the navigation property was non-nullable. Only projectId (not the full Project object) is sent.
    /// </summary>
    [Fact]
    public async Task CreateIssue_FromKanbanLane_WithStatusAndPriority_Returns201()
    {
        var (tenantId, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // This mirrors exactly what the kanban frontend sends via issueStore.createIssue
        var response = await _client.PostAsJsonAsync("/api/issues", new
        {
            title = "Kanban Issue",
            status = IssueStatus.InProgress,
            priority = IssuePriority.Medium,
            type = IssueType.Issue,
            projectId
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var issue = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Kanban Issue", issue.GetProperty("title").GetString());
        Assert.Equal(projectId.ToString(), issue.GetProperty("projectId").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetIssues_ForProject_Returns200()
    {
        var (tenantId, projectId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        db.Issues.Add(new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Existing Issue", Number = 1 });
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/issues?projectId={projectId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}
