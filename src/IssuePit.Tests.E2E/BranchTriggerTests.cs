using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests verifying that triggering a CI/CD run from the Branches tab works correctly.
///
/// Regression coverage for: clicking "Trigger Run" from the branches tab resulted in a 400
/// Bad Request because Vue's @click="triggerRun" handler passed the MouseEvent as the
/// first argument (forceWithActiveRunIds), which was serialised as {} instead of an array.
///
/// The test mocks the git and CI/CD APIs using Playwright route interception so it runs
/// without a real git repository.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class BranchTriggerTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public BranchTriggerTests(AspireFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = "chrome",
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    /// <summary>
    /// Regression test: clicking "Trigger Run" from the Branches tab must not produce a 400 Bad
    /// Request. Previously the click event object was forwarded as forceWithActiveRunIds, causing
    /// ASP.NET Core JSON binding to reject the request.
    /// </summary>
    [Fact]
    public async Task Ui_BranchTrigger_TriggerRun_DoesNotReturn400()
    {
        if (FrontendUrl is null)
            throw new InvalidOperationException("FRONTEND_URL is not set. This test requires a running frontend.");

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        var page = await context.NewPageAsync();
        context.SetDefaultTimeout(E2ETimeouts.Default);

        try
        {
            var username = $"bt{Guid.NewGuid():N}"[..12];
            const string password = "TestPass1!";

            // 1. Register via the UI and wait for redirect to dashboard
            await new LoginPage(page).RegisterAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/",
                new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

            // 2. Create org and project via API using the same session
            var tenantId = await GetDefaultTenantIdAsync();
            using var apiClient = CreateCookieClient();
            apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
            await apiClient.PostAsJsonAsync("/api/auth/login", new { username, password });

            var orgSlug = $"bt-org-{Guid.NewGuid():N}"[..16];
            var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "Branch Trigger Org", slug = orgSlug });
            Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
            var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
            var orgId = org.GetProperty("id").GetString()!;

            var projectSlug = $"bt-proj-{Guid.NewGuid():N}"[..16];
            var projResp = await apiClient.PostAsJsonAsync("/api/projects",
                new { name = "Branch Trigger Project", slug = projectSlug, orgId });
            Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
            var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
            var projectId = project.GetProperty("id").GetString()!;

            // 3. Mock the git repos endpoint so the page thinks a repo is configured
            var fakeRepoId = Guid.NewGuid().ToString();
            await page.RouteAsync($"**/api/projects/{projectId}/git/repos", async route =>
            {
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = $$"""
                        [{"id":"{{fakeRepoId}}","projectId":"{{projectId}}","remoteUrl":"https://github.com/example/repo.git",
                        "defaultBranch":"main","mode":"Working","lastFetchedAt":null}]
                        """,
                });
            });

            // 4. Mock the branches endpoint so the branches tab shows at least one branch
            await page.RouteAsync($"**/api/projects/{projectId}/git/branches", async route =>
            {
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = """[{"name":"main","sha":"abc1234567890def","isRemote":false,"commitDate":null}]""",
                });
            });

            // 5. Mock the git tree endpoint (needed when the repo is loaded)
            await page.RouteAsync($"**/api/projects/{projectId}/git/tree**", async route =>
            {
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = "[]",
                });
            });

            // 6. Mock the CI/CD runs list (used by CommitCiCdStatus component)
            await page.RouteAsync($"**/api/cicd-runs**", async route =>
            {
                if (route.Request.Method == "GET")
                {
                    await route.FulfillAsync(new RouteFulfillOptions
                    {
                        Status = 200,
                        ContentType = "application/json",
                        Body = "[]",
                    });
                }
                else
                {
                    await route.ContinueAsync();
                }
            });

            // 7. Mock the workflows endpoint used by TriggerCiCdModal
            await page.RouteAsync($"**/api/projects/{projectId}/git/workflows", async route =>
            {
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = "[]",
                });
            });

            // 8. Capture and mock the trigger endpoint — record the request body so we can assert it
            string? capturedRequestBody = null;
            int triggerResponseStatus = 0;
            await page.RouteAsync("**/api/cicd-runs/trigger", async route =>
            {
                capturedRequestBody = route.Request.PostData;
                var newRunId = Guid.NewGuid().ToString();
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 202,
                    ContentType = "application/json",
                    Body = $$"""{"runId":"{{newRunId}}","projectId":"{{projectId}}","commitSha":"main","eventName":"push"}""",
                });
                triggerResponseStatus = 202;
            });

            // 9. Navigate to the code page on the branches tab
            var codePage = new CodePage(page);
            await codePage.GotoBranchesTabAsync(projectId);

            // 10. Click the Run button for the "main" branch and wait for the trigger modal
            await codePage.ClickRunOnFirstBranchAsync();

            // 11. Click "Trigger Run" — this is the action that previously forwarded a MouseEvent
            await codePage.ClickTriggerRunAsync();

            // 12. Verify the modal closes (trigger succeeded — the component emits 'triggered')
            var modalClosed = await codePage.WaitForModalToCloseAsync();
            Assert.True(modalClosed, "Trigger modal should close after a successful trigger.");

            // 13. Verify the API was called with a valid request (not a MouseEvent serialised as {})
            Assert.NotNull(capturedRequestBody);
            using var doc = JsonDocument.Parse(capturedRequestBody!);
            var root = doc.RootElement;

            // forceWithActiveRunIds must be absent or an array — never a non-array value
            if (root.TryGetProperty("forceWithActiveRunIds", out var field))
                Assert.Equal(JsonValueKind.Array, field.ValueKind);

            // branch must be "main"
            Assert.True(root.TryGetProperty("branch", out var branchField));
            Assert.Equal("main", branchField.GetString());

            Assert.Equal(202, triggerResponseStatus);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    // --- Helpers ---

    private HttpClient CreateCookieClient()
    {
        var handler = new HttpClientHandler { UseCookies = true, CookieContainer = new System.Net.CookieContainer() };
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
}
