using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests that use the real <c>git</c> CLI to interact with the IssuePit Git Server.
/// Tests perform actual git clone, commit, and push operations over HTTP to verify
/// that the smart HTTP protocol works end-to-end with authenticated clients.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class GitServerRealClientTests
{
    private readonly AspireFixture _fixture;

    public GitServerRealClientTests(AspireFixture fixture) => _fixture = fixture;

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

    private async Task<(HttpClient client, Guid orgId, string orgSlug, string username, string password)> SetupOrgAsync()
    {
        var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"git{Guid.NewGuid():N}"[..12];
        var password = "GitTest1!";
        var registerResp = await client.PostAsJsonAsync("/api/auth/register", new { username, password });
        registerResp.EnsureSuccessStatusCode();

        var orgSlug = $"gitorg{Guid.NewGuid():N}"[..14];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "Git Real Client Org", slug = orgSlug });
        orgResp.EnsureSuccessStatusCode();
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        return (client, orgId, orgSlug, username, password);
    }

    private static (int exitCode, string stdout, string stderr) RunGit(string workingDir, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // Suppress interactive prompts — auth failures should fail immediately
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        // Disable credential helpers so only Basic Auth from the URL is used
        psi.Environment["GIT_CONFIG_COUNT"] = "1";
        psi.Environment["GIT_CONFIG_KEY_0"] = "credential.helper";
        psi.Environment["GIT_CONFIG_VALUE_0"] = "";
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Could not start git");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, stdout, stderr);
    }

    private static string BuildGitUrl(string baseUrl, string orgSlug, string repoSlug,
        string username, string password)
    {
        // Encode username/password into the URL: http://user:pass@host/org/repo.git
        var uri = new UriBuilder(baseUrl)
        {
            UserName = Uri.EscapeDataString(username),
            Password = Uri.EscapeDataString(password),
        };
        return $"{uri.Scheme}://{uri.UserName}:{uri.Password}@{uri.Host}:{uri.Port}/{orgSlug}/{repoSlug}.git";
    }

    private static bool IsGitAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("git", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit();
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Real git clone: create a repo via API, then git clone it to a temp dir.
    /// An empty bare repo should clone successfully (empty repository).
    /// </summary>
    [Fact]
    public async Task Git_Clone_EmptyRepo_Succeeds()
    {
        if (_fixture.GitServerUrl is null)
            throw new InvalidOperationException("Git server URL not available — ensure the 'git-server' Aspire resource is running.");
        if (!IsGitAvailable())
            throw new InvalidOperationException("git CLI not found on PATH — install git to run E2E git tests.");

        var (client, orgId, orgSlug, username, password) = await SetupOrgAsync();
        var slug = $"clone-{Guid.NewGuid():N}"[..14];

        // Create repo with Write access for the user (so anonymous gets nothing, but our user can clone)
        var createResp = await client.PostAsJsonAsync(
            $"/api/orgs/{orgId}/git-server/repos",
            new { slug, isTemporary = true, defaultAccessLevel = 1 }); // 1 = Read
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var tempDir = Path.Combine(Path.GetTempPath(), $"issuepit-e2e-clone-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var cloneUrl = BuildGitUrl(_fixture.GitServerUrl, orgSlug, slug, username, password);
            var cloneDir = Path.Combine(tempDir, "cloned");

            var (exitCode, stdout, stderr) = RunGit(tempDir, "clone", cloneUrl, cloneDir);

            // Exit 0 = success; git prints "warning: You appear to have cloned an empty repository." for bare repos
            Assert.True(exitCode == 0,
                $"git clone failed (exit {exitCode}).\nstdout: {stdout}\nstderr: {stderr}");
            Assert.True(Directory.Exists(cloneDir), "Clone directory should exist after successful clone.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Real git push: create a repo, clone it, make a commit, and push it back.
    /// Verifies the full write path of the smart HTTP protocol.
    /// </summary>
    [Fact]
    public async Task Git_Push_ToRepo_Succeeds()
    {
        if (_fixture.GitServerUrl is null)
            throw new InvalidOperationException("Git server URL not available — ensure the 'git-server' Aspire resource is running.");
        if (!IsGitAvailable())
            throw new InvalidOperationException("git CLI not found on PATH — install git to run E2E git tests.");

        var (client, orgId, orgSlug, username, password) = await SetupOrgAsync();
        var slug = $"push-{Guid.NewGuid():N}"[..14];

        // Create repo with Write default access so our user (who owns the org) can push
        var createResp = await client.PostAsJsonAsync(
            $"/api/orgs/{orgId}/git-server/repos",
            new { slug, isTemporary = true, defaultAccessLevel = 2 }); // 2 = Write
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var tempDir = Path.Combine(Path.GetTempPath(), $"issuepit-e2e-push-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var cloneDir = Path.Combine(tempDir, "cloned");
            var cloneUrl = BuildGitUrl(_fixture.GitServerUrl, orgSlug, slug, username, password);

            // Step 1: clone (empty repo)
            var (cloneCode, _, cloneErr) = RunGit(tempDir, "clone", cloneUrl, cloneDir);
            Assert.True(cloneCode == 0, $"git clone failed: {cloneErr}");

            // Step 2: configure git identity
            RunGit(cloneDir, "config", "user.name", "E2E Test");
            RunGit(cloneDir, "config", "user.email", "e2e@issuepit.test");

            // Step 3: create a commit
            var testFile = Path.Combine(cloneDir, "hello.txt");
            await File.WriteAllTextAsync(testFile, "Hello from IssuePit E2E test!\n");
            RunGit(cloneDir, "add", "hello.txt");
            var (commitCode, _, commitErr) = RunGit(cloneDir, "commit", "-m", "feat: initial commit from E2E test");
            Assert.True(commitCode == 0, $"git commit failed: {commitErr}");

            // Step 4: push — use HEAD:main to explicitly name the branch on the remote regardless of
            // the local default branch name configured in the git client environment.
            var (pushCode, _, pushErr) = RunGit(cloneDir, "push", "origin", "HEAD:main");
            Assert.True(pushCode == 0,
                $"git push failed (exit {pushCode}).\nstderr: {pushErr}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Unauthenticated clone on a repo with None default access should fail with 401.
    /// git CLI sees a 401 and reports authentication failure.
    /// </summary>
    [Fact]
    public async Task Git_Clone_Unauthenticated_OnPrivateRepo_Fails()
    {
        if (_fixture.GitServerUrl is null)
            throw new InvalidOperationException("Git server URL not available — ensure the 'git-server' Aspire resource is running.");
        if (!IsGitAvailable())
            throw new InvalidOperationException("git CLI not found on PATH — install git to run E2E git tests.");

        var (client, orgId, orgSlug, username, _) = await SetupOrgAsync();
        var slug = $"priv-{Guid.NewGuid():N}"[..14];

        // Create repo with None default access — anonymous users must not clone
        var createResp = await client.PostAsJsonAsync(
            $"/api/orgs/{orgId}/git-server/repos",
            new { slug, isTemporary = true, defaultAccessLevel = 0 }); // 0 = None
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var tempDir = Path.Combine(Path.GetTempPath(), $"issuepit-e2e-priv-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            // No credentials in URL — should be rejected
            var anonUrl = $"{_fixture.GitServerUrl}/{orgSlug}/{slug}.git";
            var cloneDir = Path.Combine(tempDir, "cloned");

            var (exitCode, _, _) = RunGit(tempDir, "clone", anonUrl, cloneDir);

            // Exit != 0 means git correctly rejected the unauthenticated clone
            Assert.True(exitCode != 0,
                "Unauthenticated clone of a private repo should have failed.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
