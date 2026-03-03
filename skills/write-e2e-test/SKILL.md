---
name: write-e2e-test
description: Write end-to-end tests for IssuePit using the Aspire-backed xUnit + Playwright test project in src/IssuePit.Tests.E2E. Use this skill when asked to add, extend, or fix E2E tests covering API flows, UI flows, or Kafka event assertions.
license: Apache-2.0
---

# Write E2E Test

This skill teaches you how to add or modify end-to-end tests in `src/IssuePit.Tests.E2E`. Tests run against the full Aspire stack (Postgres, Kafka, Redis, .NET API, Vue/Nuxt frontend) started once by `AspireFixture`.

## Project Overview

```
src/IssuePit.Tests.E2E/
├── AssemblyInfo.cs                  # xUnit collection — one shared fixture per run
├── AspireFixture.cs                 # Boots Aspire AppHost; exposes ApiClient, KafkaBootstrapServers, FrontendUrl
├── ApiSmokeTests.cs                 # Health/alive/OpenAPI reachability checks
├── FrontendSmokeTests.cs            # Playwright smoke tests (logged-in shared context)
├── HappyPathTests.cs                # Full API + UI happy-path flows
├── IssueKafkaNotificationTests.cs   # Kafka event assertions
└── Pages/                           # Page Object Model classes
    ├── LoginPage.cs
    ├── DashboardPage.cs
    ├── OrgsPage.cs
    ├── OrgDetailPage.cs
    ├── IssuesPage.cs
    └── MilestonesPage.cs
```

## Step-by-Step: Adding an API E2E Test

1. **Choose the right file.** Add API happy-path tests to `HappyPathTests.cs`. Add smoke/health tests to `ApiSmokeTests.cs`. Add Kafka event tests to `IssueKafkaNotificationTests.cs`.

2. **Declare the test class membership.**
   ```csharp
   [Collection("E2E")]
   [Trait("Category", "E2E")]
   public class HappyPathTests(AspireFixture fixture) { ... }
   ```

3. **Create an isolated HTTP client per test.** Always dispose it with `using`:
   ```csharp
   // Helper returns an IDisposable client — always use `using var` at the call site
   private HttpClient CreateCookieClient()
   {
       var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
       return new HttpClient(handler) { BaseAddress = fixture.ApiClient!.BaseAddress };
   }

   // Call site
   using var client = CreateCookieClient();
   ```

4. **Resolve the default tenant before any domain API call.**
   ```csharp
   private async Task<string> GetDefaultTenantIdAsync()
   {
       var resp = await fixture.ApiClient!.GetAsync("/api/admin/tenants");
       resp.EnsureSuccessStatusCode();
       var tenants = await resp.Content.ReadFromJsonAsync<JsonElement>();
       foreach (var tenant in tenants.EnumerateArray())
           if (tenant.GetProperty("hostname").GetString() == "localhost")
               return tenant.GetProperty("id").GetString()!;
       throw new InvalidOperationException("Default 'localhost' tenant not found.");
   }
   ```

5. **Register a unique user per test** using a `Guid`-based username to prevent cross-test state collisions:
   ```csharp
   var username = $"e2e{Guid.NewGuid():N}"[..12];
   const string password = "TestPass1!";
   await client.PostAsJsonAsync("/api/auth/register", new { username, password });
   ```

6. **Assert every mutating HTTP call**, not just the final read:
   ```csharp
   var resp = await client.PostAsJsonAsync("/api/orgs", new { name = "My Org", slug = orgSlug });
   Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
   ```

7. **Name the test** following the established pattern:

   | Type | Pattern | Example |
   |------|---------|---------|
   | API happy-path | `Api_HappyPath_<Scenario>` | `Api_HappyPath_CreateBoardAndLane` |
   | UI happy-path | `Ui_HappyPath_<Scenario>` | `Ui_HappyPath_CreateMilestone` |
   | Smoke check | `Api_<Resource>Endpoint_<Expectation>` | `Api_HealthEndpoint_ReturnsOk` |
   | Kafka event | `<Action>_Publishes<Event>_<Expectation>` | `CreateIssue_PublishesKafkaNotification_WithCorrectPayload` |

## Step-by-Step: Adding a UI (Playwright) E2E Test

1. **Implement `IAsyncLifetime`** on the test class to manage the Playwright browser:
   ```csharp
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
   ```

2. **Read `FrontendUrl`** from the fixture, falling back to the env var. Throw if neither is set — never fall back to a hardcoded port:
   ```csharp
   private string FrontendUrl =>
       _fixture.FrontendUrl
       ?? Environment.GetEnvironmentVariable("FRONTEND_URL")
       ?? throw new InvalidOperationException(
           "FRONTEND_URL environment variable must be set to run frontend tests");
   ```

3. **Create a `IBrowserContext` per test** (wrap in try/finally to ensure cleanup):
   ```csharp
   var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
   var page = await context.NewPageAsync();
   try { /* test logic */ }
   finally { await context.CloseAsync(); }
   ```

4. **Use Page Object Model classes** from `Pages/` — never write raw Playwright locators in test methods. If a page is not yet covered, add a new class (one class per file):
   ```csharp
   // Good
   var issuesPage = new IssuesPage(page);
   await issuesPage.GotoAsync(projectId);
   await issuesPage.CreateIssueAsync("My Issue");

   // Bad — raw locators in test methods
   await page.ClickAsync("button:has-text('New Issue')");
   ```

5. **Page Object class conventions:**
   - Accept `IPage` in the primary constructor.
   - `GotoAsync()` navigates and waits for page to settle (`NetworkIdle` + heading selector).
   - Action methods (e.g. `CreateIssueAsync`) encapsulate all UI interactions for a workflow.
   - `ILocator` properties expose stable selectors for assertions (`href`, `has-text`, `placeholder`, `autocomplete`, `type`).

   ```csharp
   public class IssuesPage(IPage page)
   {
       public async Task GotoAsync(string projectId)
       {
           await page.GotoAsync($"/projects/{projectId}/issues");
           await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
           await page.WaitForSelectorAsync("h1:has-text('Issues')", new PageWaitForSelectorOptions { Timeout = 10_000 });
       }

       public async Task CreateIssueAsync(string title)
       {
           await page.ClickAsync("button:has-text('New Issue')");
           await page.FillAsync("input[placeholder='Issue title']", title);
           await page.ClickAsync("button:has-text('Create Issue')");
           await page.WaitForSelectorAsync($"text={title}", new PageWaitForSelectorOptions { Timeout = 10_000 });
       }
   }
   ```

## Step-by-Step: Adding a Kafka Event Test

1. **Subscribe before producing** to avoid the race where a message arrives before the consumer has an assignment:
   ```csharp
   var consumerConfig = new ConsumerConfig
   {
       BootstrapServers = fixture.KafkaBootstrapServers,
       GroupId = $"e2e-test-{Guid.NewGuid():N}",
       AutoOffsetReset = AutoOffsetReset.Latest,
       EnableAutoCommit = false,
   };
   using var consumer = new ConsumerBuilder<string, string>(consumerConfig)
       .SetLogHandler((_, _) => { }) // suppress librdkafka stderr noise in test output
       .Build();
   consumer.Subscribe("issue-assigned");
   ```

2. **Wait for partition assignment** before producing any message:
   ```csharp
   using var assignWait = new CancellationTokenSource(TimeSpan.FromSeconds(15));
   while (consumer.Assignment.Count == 0 && !assignWait.Token.IsCancellationRequested)
       consumer.Consume(TimeSpan.FromMilliseconds(200));
   ```

3. **Produce** the triggering action (e.g. create an issue via the API).

4. **Poll with a timeout**, matching by message key, and handle transient `UnknownTopicOrPart`:
   ```csharp
   using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
   ConsumeResult<string, string>? result = null;
   while (!cts.Token.IsCancellationRequested)
   {
       try
       {
           var msg = consumer.Consume(TimeSpan.FromSeconds(1));
           if (msg?.Message.Key == expectedId) { result = msg; break; }
       }
       catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart) { }
   }
   consumer.Close();
   Assert.NotNull(result);
   ```

## Key Constraints

- **Never start/stop the Aspire stack** in individual tests — only `AspireFixture` manages the lifecycle.
- **Do not increase Playwright timeouts** as a workaround for flakiness — investigate the root cause.
- **Do not fall back to a hardcoded `localhost` port** when `FrontendUrl` is null — throw `InvalidOperationException` with a clear message.
- **Do not share state between tests** — each test creates its own user, org, and project with unique `Guid`-based slugs.
- **Every new UI feature must include at least one positive E2E test** covering create, list, and interact flows.
- **Helpers (`CreateCookieClient`, `GetDefaultTenantIdAsync`) live as private methods** in each test class; duplicate rather than creating a shared base class unless multiple classes need them.
