using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class IssueHistoryEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
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
    public async Task CreateIssue_RecordsCreatedHistoryEvent()
    {
        var (tenantId, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var createResponse = await _client.PostAsJsonAsync("/api/issues",
            new { title = "History Test Issue", projectId, status = IssueStatus.Backlog, priority = IssuePriority.NoPriority, type = IssueType.Issue });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var issue = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var issueId = issue.GetProperty("id").GetString();

        var historyResponse = await _client.GetAsync($"/api/issues/{issueId}/history");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        var history = await historyResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(history);
        Assert.Single(history);
        Assert.Equal("created", history[0].GetProperty("eventType").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task UpdateIssue_Status_RecordsStatusChangedHistoryEvent()
    {
        var (tenantId, projectId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Status Test", Number = 1 };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var updateResponse = await _client.PutAsJsonAsync($"/api/issues/{issue.Id}", new { status = "in_progress" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var historyResponse = await _client.GetAsync($"/api/issues/{issue.Id}/history");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        var history = await historyResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(history);
        Assert.Single(history);
        Assert.Equal("status_changed", history[0].GetProperty("eventType").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task UpdateIssue_WithSameValues_DoesNotRecordHistoryEvents()
    {
        var (tenantId, projectId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "No Change Test", Status = IssueStatus.Backlog, Number = 1 };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Send update with same status (Backlog) — no change should be recorded
        var updateResponse = await _client.PutAsJsonAsync($"/api/issues/{issue.Id}", new { status = "backlog" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var historyResponse = await _client.GetAsync($"/api/issues/{issue.Id}/history");
        var history = await historyResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(history);
        Assert.Empty(history);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task AddLabel_RecordsLabelAddedHistoryEvent()
    {
        var (tenantId, projectId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Label Test", Number = 1 };
        db.Issues.Add(issue);
        var label = new Label { Id = Guid.NewGuid(), ProjectId = projectId, Name = "bug", Color = "#ff0000" };
        db.Labels.Add(label);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var addResponse = await _client.PostAsJsonAsync($"/api/issues/{issue.Id}/labels", new { labelId = label.Id });
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

        var historyResponse = await _client.GetAsync($"/api/issues/{issue.Id}/history");
        var history = await historyResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(history);
        Assert.Single(history);
        Assert.Equal("label_added", history[0].GetProperty("eventType").GetString());
        Assert.Equal("bug", history[0].GetProperty("newValue").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}
