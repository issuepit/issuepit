using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class IssueTaskEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid projectId, Guid issueId)> SeedIssueAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var issueId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Hostname = $"host-{tenantId}" });
        db.Organizations.Add(new Organization { Id = orgId, TenantId = tenantId, Name = "Org", Slug = $"org-{orgId}" });
        db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Project", Slug = $"proj-{projectId}" });
        db.Issues.Add(new Issue { Id = issueId, ProjectId = projectId, Title = "Parent issue", Number = 1 });
        await db.SaveChangesAsync();

        return (tenantId, projectId, issueId);
    }

    [Fact]
    public async Task GetTasks_ForNewIssue_ReturnsEmptyList()
    {
        var (tenantId, _, issueId) = await SeedIssueAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/issues/{issueId}/tasks");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tasks = await response.Content.ReadFromJsonAsync<List<object>>();
        Assert.NotNull(tasks);
        Assert.Empty(tasks);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task CreateTask_WithValidIssue_Returns201()
    {
        var (tenantId, _, issueId) = await SeedIssueAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync($"/api/issues/{issueId}/tasks", new
        {
            title = "Write unit tests",
            body = "Cover the happy path",
            status = "todo",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Write unit tests", body.GetProperty("title").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task CreateTask_ForNonExistentIssue_Returns404()
    {
        var (tenantId, _, _) = await SeedIssueAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync($"/api/issues/{Guid.NewGuid()}/tasks", new
        {
            title = "Ghost task",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task UpdateTask_Returns200()
    {
        var (tenantId, _, issueId) = await SeedIssueAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var createResp = await _client.PostAsJsonAsync($"/api/issues/{issueId}/tasks", new
        {
            title = "Original title",
            status = "todo",
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var taskId = created.GetProperty("id").GetString();

        var updateResp = await _client.PutAsJsonAsync($"/api/issues/{issueId}/tasks/{taskId}", new
        {
            title = "Updated title",
            status = "in_progress",
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await updateResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Updated title", updated.GetProperty("title").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task DeleteTask_Returns204()
    {
        var (tenantId, _, issueId) = await SeedIssueAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var createResp = await _client.PostAsJsonAsync($"/api/issues/{issueId}/tasks", new
        {
            title = "To be deleted",
            status = "todo",
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var taskId = created.GetProperty("id").GetString();

        var deleteResp = await _client.DeleteAsync($"/api/issues/{issueId}/tasks/{taskId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetIssue_WithSubIssues_ReturnsParentIssueWithSubIssues()
    {
        var (tenantId, projectId, parentIssueId) = await SeedIssueAsync();

        // Seed a sub-issue directly
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            db.Issues.Add(new Issue
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "Sub-issue title",
                Number = 2,
                ParentIssueId = parentIssueId,
            });
            await db.SaveChangesAsync();
        }

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/issues/{parentIssueId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var subIssues = body.GetProperty("subIssues");
        Assert.Equal(1, subIssues.GetArrayLength());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetSubIssues_Returns200WithSubIssue()
    {
        var (tenantId, projectId, parentIssueId) = await SeedIssueAsync();

        // Seed the sub-issue directly to avoid testing the CreateIssue endpoint
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            db.Issues.Add(new Issue
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = "Child issue",
                Number = 2,
                ParentIssueId = parentIssueId,
            });
            await db.SaveChangesAsync();
        }

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/issues/{parentIssueId}/sub-issues");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var subIssues = await response.Content.ReadFromJsonAsync<List<object>>();
        Assert.NotNull(subIssues);
        Assert.NotEmpty(subIssues);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}
