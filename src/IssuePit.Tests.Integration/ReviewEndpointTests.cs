using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class ReviewEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid orgId, Guid projectId, Guid issueId)> SeedIssueWithBranchAsync(
        string? branch = "feature/test-branch",
        string? githubRepo = "owner/repo")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"host-{tenantId}" });

        var org = new Organization { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Org", Slug = $"org-{tenantId}" };
        db.Organizations.Add(org);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrgId = org.Id,
            Name = "Proj",
            Slug = $"proj-{tenantId}",
            GitHubRepo = githubRepo
        };
        db.Projects.Add(project);

        var issue = new Issue
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "Test Issue",
            Number = 1,
            Status = IssueStatus.InReview,
            GitBranch = branch
        };
        db.Issues.Add(issue);

        await db.SaveChangesAsync();
        return (tenantId, org.Id, project.Id, issue.Id);
    }

    // ── Comments ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetComments_WithValidIssue_Returns200()
    {
        var (tenantId, _, _, issueId) = await SeedIssueWithBranchAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/issues/{issueId}/review/comments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task PostComment_PrLevel_Returns201()
    {
        var (tenantId, _, _, issueId) = await SeedIssueWithBranchAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync(
            $"/api/issues/{issueId}/review/comments",
            new { body = "Looks good overall!" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task PostComment_FileLevel_Returns201()
    {
        var (tenantId, _, _, issueId) = await SeedIssueWithBranchAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync(
            $"/api/issues/{issueId}/review/comments",
            new
            {
                body = "This file looks fine.",
                filePath = "src/Program.cs",
                lineStart = 10,
                lineEnd = 12,
                diffSide = "right",
                commitSha = "abc123"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task DeleteComment_Returns204()
    {
        var (tenantId, _, _, issueId) = await SeedIssueWithBranchAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Create a comment first
        var createResponse = await _client.PostAsJsonAsync(
            $"/api/issues/{issueId}/review/comments",
            new { body = "Comment to delete" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var comment = await createResponse.Content.ReadFromJsonAsync<CommentResponse>();
        Assert.NotNull(comment);

        var deleteResponse = await _client.DeleteAsync($"/api/issues/{issueId}/review/comments/{comment.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetDiff_WithNoBranch_Returns400()
    {
        var (tenantId, _, _, issueId) = await SeedIssueWithBranchAsync(branch: null);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/issues/{issueId}/review/diff");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetDiff_WithNoGitHubRepo_Returns400()
    {
        var (tenantId, _, _, issueId) = await SeedIssueWithBranchAsync(githubRepo: null);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/issues/{issueId}/review/diff");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetComments_UnknownIssue_Returns404()
    {
        var tenantId = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T2", Hostname = $"host2-{tenantId}" });
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/issues/{Guid.NewGuid()}/review/comments");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    private sealed record CommentResponse(Guid Id, Guid IssueId, string Body);
}
