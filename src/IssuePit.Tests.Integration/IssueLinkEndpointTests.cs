using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class IssueLinkEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid projectId, Guid issueId1, Guid issueId2)> SeedTwoIssuesAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"host-{tenantId}" });

        var org = new Organization { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Org", Slug = $"org-{tenantId}" };
        db.Organizations.Add(org);

        var project = new Project { Id = Guid.NewGuid(), OrgId = org.Id, Name = "Proj", Slug = $"proj-{tenantId}" };
        db.Projects.Add(project);

        var issue1 = new Issue { Id = Guid.NewGuid(), ProjectId = project.Id, Title = "Issue A", Number = 1 };
        var issue2 = new Issue { Id = Guid.NewGuid(), ProjectId = project.Id, Title = "Issue B", Number = 2 };
        db.Issues.AddRange(issue1, issue2);

        await db.SaveChangesAsync();
        return (tenantId, project.Id, issue1.Id, issue2.Id);
    }

    [Fact]
    public async Task AddLink_Returns201AndLinkIsRetrievable()
    {
        var (tenantId, _, issueId1, issueId2) = await SeedTwoIssuesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync(
            $"/api/issues/{issueId1}/links",
            new { targetIssueId = issueId2, linkType = "blocks" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var link = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(issueId2.ToString(), link.GetProperty("targetIssueId").GetString());
        Assert.Equal("blocks", link.GetProperty("linkType").GetString());

        // Verify it appears in the GET links endpoint
        var getResp = await _client.GetAsync($"/api/issues/{issueId1}/links");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var links = await getResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, links.GetArrayLength());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task AddLink_DuplicateLinkType_Returns409()
    {
        var (tenantId, _, issueId1, issueId2) = await SeedTwoIssuesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        await _client.PostAsJsonAsync(
            $"/api/issues/{issueId1}/links",
            new { targetIssueId = issueId2, linkType = "linked_to" });

        var duplicate = await _client.PostAsJsonAsync(
            $"/api/issues/{issueId1}/links",
            new { targetIssueId = issueId2, linkType = "linked_to" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task AddLink_ToSelf_Returns400()
    {
        var (tenantId, _, issueId1, _) = await SeedTwoIssuesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync(
            $"/api/issues/{issueId1}/links",
            new { targetIssueId = issueId1, linkType = "linked_to" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task RemoveLink_Returns204AndLinkIsGone()
    {
        var (tenantId, _, issueId1, issueId2) = await SeedTwoIssuesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var addResp = await _client.PostAsJsonAsync(
            $"/api/issues/{issueId1}/links",
            new { targetIssueId = issueId2, linkType = "solves" });

        Assert.Equal(HttpStatusCode.Created, addResp.StatusCode);
        var link = await addResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var linkId = link.GetProperty("id").GetString()!;

        var deleteResp = await _client.DeleteAsync($"/api/issues/{issueId1}/links/{linkId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var getResp = await _client.GetAsync($"/api/issues/{issueId1}/links");
        var links = await getResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(0, links.GetArrayLength());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetLinks_ReturnsTargetIssueDetails()
    {
        var (tenantId, _, issueId1, issueId2) = await SeedTwoIssuesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        await _client.PostAsJsonAsync(
            $"/api/issues/{issueId1}/links",
            new { targetIssueId = issueId2, linkType = "caused_by" });

        var getResp = await _client.GetAsync($"/api/issues/{issueId1}/links");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var links = await getResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var first = links.EnumerateArray().First();
        Assert.Equal(JsonValueKind.Object, first.GetProperty("targetIssue").ValueKind);
        Assert.Equal("Issue B", first.GetProperty("targetIssue").GetProperty("title").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}
