using System.Net;
using System.Net.Http.Json;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests covering the multiple-git-origins feature (API level).
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class GitOriginsTests
{
    private readonly AspireFixture _fixture;

    public GitOriginsTests(AspireFixture fixture) => _fixture = fixture;

    private HttpClient CreateCookieClient()
    {
        var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
        return new HttpClient(handler) { BaseAddress = _fixture.ApiClient!.BaseAddress };
    }

    private async Task<string> GetDefaultTenantIdAsync()
    {
        var resp = await _fixture.ApiClient!.GetAsync("/api/admin/tenants");
        resp.EnsureSuccessStatusCode();
        var tenants = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        foreach (var tenant in tenants.EnumerateArray())
        {
            if (tenant.GetProperty("hostname").GetString() == "localhost")
                return tenant.GetProperty("id").GetString()!;
        }
        throw new InvalidOperationException("Default 'localhost' tenant not found.");
    }

    private async Task<(HttpClient client, Guid projectId)> SetupProjectAsync()
    {
        var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"git-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "Git Test Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projSlug = $"git-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects", new { name = "Git Test Project", slug = projSlug, orgId });
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = Guid.Parse(project.GetProperty("id").GetString()!);

        return (client, projectId);
    }

    /// <summary>API: create a Working-mode git origin and verify it is returned in the list.</summary>
    [Fact]
    public async Task Api_AddGitOrigin_AppearsInList()
    {
        var (client, projectId) = await SetupProjectAsync();

        var addResp = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/git/repos",
            new { remoteUrl = "https://github.com/test/repo.git", defaultBranch = "main", mode = "Working" });

        Assert.Equal(HttpStatusCode.Created, addResp.StatusCode);
        var created = await addResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("https://github.com/test/repo.git", created.GetProperty("remoteUrl").GetString());
        Assert.Equal("Working", created.GetProperty("mode").GetString());

        var listResp = await client.GetAsync($"/api/projects/{projectId}/git/repos");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, list.GetArrayLength());
    }

    /// <summary>API: add multiple git origins with different modes for one project.</summary>
    [Fact]
    public async Task Api_MultipleGitOrigins_DifferentModes()
    {
        var (client, projectId) = await SetupProjectAsync();

        await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/git/repos",
            new { remoteUrl = "https://github.com/test/repo.git", defaultBranch = "main", mode = "Working" });

        await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/git/repos",
            new { remoteUrl = "https://github.com/mirror/repo.git", defaultBranch = "main", mode = "ReadOnly" });

        await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/git/repos",
            new { remoteUrl = "https://releases.example.com/repo.git", defaultBranch = "main", mode = "Release" });

        var listResp = await client.GetAsync($"/api/projects/{projectId}/git/repos");
        var list = await listResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(3, list.GetArrayLength());

        var modes = list.EnumerateArray().Select(r => r.GetProperty("mode").GetString()).ToHashSet();
        Assert.Contains("Working", modes);
        Assert.Contains("ReadOnly", modes);
        Assert.Contains("Release", modes);
    }

    /// <summary>API: update an existing git origin's mode and URL.</summary>
    [Fact]
    public async Task Api_UpdateGitOrigin_ChangesMode()
    {
        var (client, projectId) = await SetupProjectAsync();

        var addResp = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/git/repos",
            new { remoteUrl = "https://github.com/test/repo.git", defaultBranch = "main", mode = "Working" });
        var created = await addResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var repoId = created.GetProperty("id").GetString()!;

        var updateResp = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/git/repos/{repoId}",
            new { remoteUrl = "https://github.com/test/repo.git", defaultBranch = "develop", mode = "Release" });

        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await updateResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Release", updated.GetProperty("mode").GetString());
        Assert.Equal("develop", updated.GetProperty("defaultBranch").GetString());
    }

    /// <summary>API: delete a git origin removes it from the list.</summary>
    [Fact]
    public async Task Api_DeleteGitOrigin_RemovedFromList()
    {
        var (client, projectId) = await SetupProjectAsync();

        var addResp = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/git/repos",
            new { remoteUrl = "https://github.com/test/repo.git", defaultBranch = "main", mode = "ReadOnly" });
        var created = await addResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var repoId = created.GetProperty("id").GetString()!;

        var deleteResp = await client.DeleteAsync($"/api/projects/{projectId}/git/repos/{repoId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var listResp = await client.GetAsync($"/api/projects/{projectId}/git/repos");
        var list = await listResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(0, list.GetArrayLength());
    }
}
