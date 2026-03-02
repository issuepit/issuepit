using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class CodeReviewCommentEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid issueId)> SeedIssueAsync()
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
        db.Issues.Add(new Issue { Id = issueId, ProjectId = projectId, Title = "Review issue", Number = 1 });
        await db.SaveChangesAsync();

        return (tenantId, issueId);
    }

    [Fact]
    public async Task GetCodeReviewComments_ForNewIssue_ReturnsEmptyList()
    {
        var (tenantId, issueId) = await SeedIssueAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/issues/{issueId}/code-review-comments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var comments = await response.Content.ReadFromJsonAsync<List<object>>();
        Assert.NotNull(comments);
        Assert.Empty(comments);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task AddCodeReviewComment_WithValidData_Returns201()
    {
        var (tenantId, issueId) = await SeedIssueAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync($"/api/issues/{issueId}/code-review-comments", new
        {
            filePath = "src/app.ts",
            startLine = 10,
            endLine = 15,
            sha = "abc1234",
            snippet = "const x = 1;",
            contextBefore = "// previous code",
            contextAfter = "// next code",
            body = "Consider using a constant here."
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("src/app.ts", body.GetProperty("filePath").GetString());
        Assert.Equal(10, body.GetProperty("startLine").GetInt32());
        Assert.Equal(15, body.GetProperty("endLine").GetInt32());
        Assert.Equal("abc1234", body.GetProperty("sha").GetString());
        Assert.Equal("// previous code", body.GetProperty("contextBefore").GetString());
        Assert.Equal("// next code", body.GetProperty("contextAfter").GetString());
        Assert.Equal("Consider using a constant here.", body.GetProperty("body").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task AddCodeReviewComment_ForNonExistentIssue_Returns404()
    {
        var (tenantId, _) = await SeedIssueAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync($"/api/issues/{Guid.NewGuid()}/code-review-comments", new
        {
            filePath = "src/app.ts",
            startLine = 1,
            endLine = 1,
            sha = "abc1234",
            body = "Comment on missing issue."
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task AddCodeReviewCommentsBatch_WithValidData_Returns200WithAllComments()
    {
        var (tenantId, issueId) = await SeedIssueAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync($"/api/issues/{issueId}/code-review-comments/batch", new[]
        {
            new { filePath = "src/a.ts", startLine = 1, endLine = 3, sha = "abc1234", snippet = "line 1", body = "Comment A" },
            new { filePath = "src/b.ts", startLine = 5, endLine = 5, sha = "abc1234", snippet = "line 5", body = "Comment B" },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var comments = await response.Content.ReadFromJsonAsync<List<System.Text.Json.JsonElement>>();
        Assert.NotNull(comments);
        Assert.Equal(2, comments.Count);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetCodeReviewComments_AfterAdding_ReturnsSavedComments()
    {
        var (tenantId, issueId) = await SeedIssueAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        await _client.PostAsJsonAsync($"/api/issues/{issueId}/code-review-comments", new
        {
            filePath = "README.md",
            startLine = 20,
            endLine = 20,
            sha = "deadbeef",
            body = "Typo here."
        });

        var response = await _client.GetAsync($"/api/issues/{issueId}/code-review-comments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var comments = await response.Content.ReadFromJsonAsync<List<System.Text.Json.JsonElement>>();
        Assert.NotNull(comments);
        Assert.Single(comments);
        Assert.Equal("README.md", comments[0].GetProperty("filePath").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}
