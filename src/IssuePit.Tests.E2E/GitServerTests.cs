using System.Net;
using System.Net.Http.Json;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests covering the Git Server REST API (repository management, permissions, branch protections).
/// These are API-level tests that do not require a running git client.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class GitServerTests
{
    private readonly AspireFixture _fixture;

    public GitServerTests(AspireFixture fixture) => _fixture = fixture;

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

    private async Task<(HttpClient client, Guid orgId)> SetupOrgAsync()
    {
        var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"gs{Guid.NewGuid():N}"[..12];
        var registerResp = await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });
        registerResp.EnsureSuccessStatusCode();

        var orgSlug = $"gs-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "GitServer Test Org", slug = orgSlug });
        orgResp.EnsureSuccessStatusCode();
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        return (client, orgId);
    }

    /// <summary>API: create a git server repo and verify it appears in the list.</summary>
    [Fact]
    public async Task Api_CreateGitServerRepo_AppearsInList()
    {
        var (client, orgId) = await SetupOrgAsync();

        var slug = $"repo-{Guid.NewGuid():N}"[..16];
        var createResp = await client.PostAsJsonAsync(
            $"/api/orgs/{orgId}/git-server/repos",
            new { slug, description = "Test repository", defaultBranch = "main", isTemporary = false, defaultAccessLevel = 1 });

        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(slug, created.GetProperty("slug").GetString());
        Assert.Equal("main", created.GetProperty("defaultBranch").GetString());

        var listResp = await client.GetAsync($"/api/orgs/{orgId}/git-server/repos");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal(slug, list[0].GetProperty("slug").GetString());
    }

    /// <summary>API: delete a git server repo and verify it no longer appears in the list.</summary>
    [Fact]
    public async Task Api_DeleteGitServerRepo_DisappearsFromList()
    {
        var (client, orgId) = await SetupOrgAsync();

        var slug = $"repo-{Guid.NewGuid():N}"[..16];
        var createResp = await client.PostAsJsonAsync(
            $"/api/orgs/{orgId}/git-server/repos",
            new { slug, isTemporary = false, defaultAccessLevel = 1 });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var repoId = created.GetProperty("id").GetString()!;

        var deleteResp = await client.DeleteAsync($"/api/orgs/{orgId}/git-server/repos/{repoId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var listResp = await client.GetAsync($"/api/orgs/{orgId}/git-server/repos");
        var list = await listResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(0, list.GetArrayLength());
    }

    /// <summary>API: grant and revoke a permission on a git server repo.</summary>
    [Fact]
    public async Task Api_GrantAndRevokePermission_Works()
    {
        var (client, orgId) = await SetupOrgAsync();

        var slug = $"repo-{Guid.NewGuid():N}"[..16];
        var createResp = await client.PostAsJsonAsync(
            $"/api/orgs/{orgId}/git-server/repos",
            new { slug, isTemporary = false, defaultAccessLevel = 0 });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var repoId = Guid.Parse(created.GetProperty("id").GetString()!);

        // Grant write permission for a user (using a fake user id for API test)
        var userId = Guid.NewGuid();
        var grantResp = await client.PostAsJsonAsync(
            $"/api/orgs/{orgId}/git-server/repos/{repoId}/permissions",
            new { userId, accessLevel = 2 });
        Assert.Equal(HttpStatusCode.Created, grantResp.StatusCode);
        var perm = await grantResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var permId = perm.GetProperty("id").GetString()!;

        var listPermsResp = await client.GetAsync($"/api/orgs/{orgId}/git-server/repos/{repoId}/permissions");
        var perms = await listPermsResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, perms.GetArrayLength());

        var revokeResp = await client.DeleteAsync($"/api/orgs/{orgId}/git-server/repos/{repoId}/permissions/{permId}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResp.StatusCode);

        var listAfterResp = await client.GetAsync($"/api/orgs/{orgId}/git-server/repos/{repoId}/permissions");
        var permsAfter = await listAfterResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(0, permsAfter.GetArrayLength());
    }

    /// <summary>API: create and delete branch protection rules.</summary>
    [Fact]
    public async Task Api_CreateAndDeleteBranchProtection_Works()
    {
        var (client, orgId) = await SetupOrgAsync();

        var slug = $"repo-{Guid.NewGuid():N}"[..16];
        var createResp = await client.PostAsJsonAsync(
            $"/api/orgs/{orgId}/git-server/repos",
            new { slug, isTemporary = false, defaultAccessLevel = 1 });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var repoId = Guid.Parse(created.GetProperty("id").GetString()!);

        var bpResp = await client.PostAsJsonAsync(
            $"/api/orgs/{orgId}/git-server/repos/{repoId}/branch-protections",
            new { pattern = "main", disallowForcePush = true, requirePullRequest = false, allowAdminBypass = true });
        Assert.Equal(HttpStatusCode.Created, bpResp.StatusCode);
        var bp = await bpResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("main", bp.GetProperty("pattern").GetString());
        Assert.True(bp.GetProperty("disallowForcePush").GetBoolean());
        var ruleId = bp.GetProperty("id").GetString()!;

        var listResp = await client.GetAsync($"/api/orgs/{orgId}/git-server/repos/{repoId}/branch-protections");
        var rules = await listResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, rules.GetArrayLength());

        var deleteResp = await client.DeleteAsync($"/api/orgs/{orgId}/git-server/repos/{repoId}/branch-protections/{ruleId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var listAfterResp = await client.GetAsync($"/api/orgs/{orgId}/git-server/repos/{repoId}/branch-protections");
        var rulesAfter = await listAfterResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(0, rulesAfter.GetArrayLength());
    }

    /// <summary>API: duplicate slug within org is rejected.</summary>
    [Fact]
    public async Task Api_DuplicateRepoSlug_ReturnsConflict()
    {
        var (client, orgId) = await SetupOrgAsync();

        var slug = $"repo-{Guid.NewGuid():N}"[..16];
        await client.PostAsJsonAsync(
            $"/api/orgs/{orgId}/git-server/repos",
            new { slug, isTemporary = false, defaultAccessLevel = 1 });

        var dupResp = await client.PostAsJsonAsync(
            $"/api/orgs/{orgId}/git-server/repos",
            new { slug, isTemporary = false, defaultAccessLevel = 1 });

        Assert.Equal(HttpStatusCode.Conflict, dupResp.StatusCode);
    }

    /// <summary>API: set repo read-only flag.</summary>
    [Fact]
    public async Task Api_SetReadOnly_Works()
    {
        var (client, orgId) = await SetupOrgAsync();

        var slug = $"repo-{Guid.NewGuid():N}"[..16];
        var createResp = await client.PostAsJsonAsync(
            $"/api/orgs/{orgId}/git-server/repos",
            new { slug, isTemporary = false, defaultAccessLevel = 1 });
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var repoId = Guid.Parse(created.GetProperty("id").GetString()!);

        var setResp = await client.PostAsJsonAsync(
            $"/api/orgs/{orgId}/git-server/repos/{repoId}/read-only",
            new { isReadOnly = true });
        Assert.Equal(HttpStatusCode.NoContent, setResp.StatusCode);
    }
}
