using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the config-repo sync feature against the full Aspire stack.
/// Tests create org/project via API, write local config files, trigger sync,
/// and verify the resulting state via the API.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class ConfigRepoTests
{
    private readonly AspireFixture _fixture;

    public ConfigRepoTests(AspireFixture fixture) => _fixture = fixture;

    private HttpClient CreateCookieClient()
    {
        var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
        return new HttpClient(handler) { BaseAddress = _fixture.ApiClient!.BaseAddress };
    }

    private async Task<string> GetDefaultTenantIdAsync()
    {
        var resp = await _fixture.ApiClient!.GetAsync("/api/admin/tenants");
        resp.EnsureSuccessStatusCode();
        var tenants = await resp.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var tenant in tenants.EnumerateArray())
        {
            if (tenant.GetProperty("hostname").GetString() == "localhost")
                return tenant.GetProperty("id").GetString()!;
        }
        throw new InvalidOperationException("Default 'localhost' tenant not found.");
    }

    private static void WriteJson(string dir, string subDir, string fileName, object content)
    {
        var fullDir = Path.Combine(dir, subDir);
        Directory.CreateDirectory(fullDir);
        File.WriteAllText(
            Path.Combine(fullDir, fileName),
            JsonSerializer.Serialize(content, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    /// <summary>
    /// Full flow: create org + project via API, write a local config dir, configure the
    /// config-repo URL via the API, trigger sync, and verify the org and project were updated.
    /// </summary>
    [Fact]
    public async Task Api_ConfigRepoSync_UpdatesOrgAndProject()
    {
        using var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        // 1. Register a user and create org + project
        var username = $"e2ecfg{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"cfg-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "Config Test Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projSlug = $"cfg-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects", new { name = "Config Test Project", slug = projSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        // 2. Write local config files
        var configDir = Path.Combine(Path.GetTempPath(), $"issuepit-e2e-cfg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(configDir);
        try
        {
            WriteJson(configDir, "orgs", $"{orgSlug}.json", new
            {
                name = "Updated via Config Repo",
                maxConcurrentRunners = 3,
                actRunnerImage = "ubuntu:22.04"
            });

            WriteJson(configDir, "projects", $"{projSlug}.json", new
            {
                orgSlug,
                description = "Applied from config repo",
                maxConcurrentRunners = 1,
                gitUrl = "https://github.com/example/config-repo-test.git",
                defaultBranch = "main"
            });

            // 3. Configure the config-repo URL
            var cfgResp = await _fixture.ApiClient!.PutAsJsonAsync(
                $"/api/admin/tenants/{tenantId}/config-repo",
                new { url = configDir, token = (string?)null, username = (string?)null, strictMode = false });
            Assert.Equal(HttpStatusCode.OK, cfgResp.StatusCode);

            // Verify GET returns what we set
            var getCfgResp = await _fixture.ApiClient!.GetAsync($"/api/admin/tenants/{tenantId}/config-repo");
            Assert.Equal(HttpStatusCode.OK, getCfgResp.StatusCode);
            var cfg = await getCfgResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(configDir, cfg.GetProperty("url").GetString());
            Assert.False(cfg.GetProperty("strictMode").GetBoolean());

            // 4. Trigger sync
            var syncResp = await _fixture.ApiClient!.PostAsync(
                $"/api/admin/tenants/{tenantId}/config-repo/sync", null);
            Assert.Equal(HttpStatusCode.OK, syncResp.StatusCode);

            // 5. Verify org was updated
            var orgGetResp = await client.GetAsync($"/api/orgs/{orgId}");
            Assert.Equal(HttpStatusCode.OK, orgGetResp.StatusCode);
            var updatedOrg = await orgGetResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Updated via Config Repo", updatedOrg.GetProperty("name").GetString());
            Assert.Equal(3, updatedOrg.GetProperty("maxConcurrentRunners").GetInt32());
            Assert.Equal("ubuntu:22.04", updatedOrg.GetProperty("actRunnerImage").GetString());

            // 6. Verify project was updated
            var projGetResp = await client.GetAsync($"/api/projects/{projectId}");
            Assert.Equal(HttpStatusCode.OK, projGetResp.StatusCode);
            var updatedProj = await projGetResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Applied from config repo", updatedProj.GetProperty("description").GetString());
            Assert.Equal(1, updatedProj.GetProperty("maxConcurrentRunners").GetInt32());

            // 7. Verify git repo was created
            var gitRepos = await client.GetAsync($"/api/projects/{projectId}/git/repos");
            Assert.Equal(HttpStatusCode.OK, gitRepos.StatusCode);
            var repos = await gitRepos.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, repos.GetArrayLength());
            Assert.Equal("https://github.com/example/config-repo-test.git", repos[0].GetProperty("remoteUrl").GetString());
        }
        finally
        {
            // Clean up config-repo setting so other tests aren't affected
            await _fixture.ApiClient!.PutAsJsonAsync(
                $"/api/admin/tenants/{tenantId}/config-repo",
                new { url = (string?)null, token = (string?)null, username = (string?)null, strictMode = false });
            Directory.Delete(configDir, recursive: true);
        }
    }

    /// <summary>
    /// Sync with strict mode: member referencing a non-existent username should be skipped,
    /// but the sync should still succeed and other fields should still be applied.
    /// </summary>
    [Fact]
    public async Task Api_ConfigRepoSync_StrictMode_UnknownUserSkipped()
    {
        using var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2estrict{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"strict-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "Strict Test Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var configDir = Path.Combine(Path.GetTempPath(), $"issuepit-e2e-strict-{Guid.NewGuid():N}");
        Directory.CreateDirectory(configDir);
        try
        {
            WriteJson(configDir, "orgs", $"{orgSlug}.json", new
            {
                name = "Strict Updated",
                members = new[] { new { username = "totally_nonexistent_user_xyz", role = "admin" } }
            });

            await _fixture.ApiClient!.PutAsJsonAsync(
                $"/api/admin/tenants/{tenantId}/config-repo",
                new { url = configDir, token = (string?)null, username = (string?)null, strictMode = true });

            // Sync should succeed even with the unknown member in strict mode — it is logged and skipped
            var syncResp = await _fixture.ApiClient!.PostAsync(
                $"/api/admin/tenants/{tenantId}/config-repo/sync", null);
            Assert.Equal(HttpStatusCode.OK, syncResp.StatusCode);

            // Org name should still be updated
            var orgGetResp = await client.GetAsync($"/api/orgs/{orgId}");
            var updatedOrg = await orgGetResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Strict Updated", updatedOrg.GetProperty("name").GetString());

            // No member should have been added for the nonexistent user
            var membersResp = await client.GetAsync($"/api/orgs/{orgId}/members");
            var members = await membersResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.DoesNotContain(members.EnumerateArray(), m =>
                m.TryGetProperty("user", out var u) &&
                u.GetProperty("username").GetString() == "totally_nonexistent_user_xyz");
        }
        finally
        {
            await _fixture.ApiClient!.PutAsJsonAsync(
                $"/api/admin/tenants/{tenantId}/config-repo",
                new { url = (string?)null, token = (string?)null, username = (string?)null, strictMode = false });
            Directory.Delete(configDir, recursive: true);
        }
    }

    /// <summary>
    /// Config-repo API: GET returns default empty state, PUT updates it, and it persists.
    /// </summary>
    [Fact]
    public async Task Api_ConfigRepo_GetPutPersists()
    {
        var tenantResp = await _fixture.ApiClient!.PostAsJsonAsync(
            "/api/admin/tenants",
            new { name = "Config Persist Test", hostname = $"cfgpersist-{Guid.NewGuid():N}.test", provisionDatabase = false });
        Assert.Equal(HttpStatusCode.Created, tenantResp.StatusCode);
        var tenant = await tenantResp.Content.ReadFromJsonAsync<JsonElement>();
        var tenantId = tenant.GetProperty("id").GetString()!;

        // GET — initially empty
        var getResp = await _fixture.ApiClient!.GetAsync($"/api/admin/tenants/{tenantId}/config-repo");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var initial = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, initial.GetProperty("url").ValueKind);
        Assert.False(initial.GetProperty("strictMode").GetBoolean());

        // PUT — update
        var putResp = await _fixture.ApiClient!.PutAsJsonAsync(
            $"/api/admin/tenants/{tenantId}/config-repo",
            new { url = "/some/path", token = "mytoken", username = "myuser", strictMode = true });
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        // GET — verify persistence
        var getResp2 = await _fixture.ApiClient!.GetAsync($"/api/admin/tenants/{tenantId}/config-repo");
        var updated = await getResp2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("/some/path", updated.GetProperty("url").GetString());
        Assert.True(updated.GetProperty("strictMode").GetBoolean());
    }

    /// <summary>Sync with no config-repo configured returns 400 Bad Request.</summary>
    [Fact]
    public async Task Api_ConfigRepoSync_NoConfigured_ReturnsBadRequest()
    {
        // Create a fresh tenant with no config-repo set
        var tenantResp = await _fixture.ApiClient!.PostAsJsonAsync(
            "/api/admin/tenants",
            new { name = "No Config Tenant", hostname = $"noconfig-{Guid.NewGuid():N}.test", provisionDatabase = false });
        var tenant = await tenantResp.Content.ReadFromJsonAsync<JsonElement>();
        var tenantId = tenant.GetProperty("id").GetString()!;

        var syncResp = await _fixture.ApiClient!.PostAsync(
            $"/api/admin/tenants/{tenantId}/config-repo/sync", null);
        Assert.Equal(HttpStatusCode.BadRequest, syncResp.StatusCode);
    }
}
