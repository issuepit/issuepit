# E2E Test Guidelines

This folder contains end-to-end tests for IssuePit, running against the full Aspire stack (Postgres, Kafka, Redis, API, Vue frontend). Use this document as the authoritative reference when adding or modifying E2E tests.

---

## Project Structure

```
IssuePit.Tests.E2E/
├── AssemblyInfo.cs              # xUnit collection definition — one shared AspireFixture per run
├── AspireFixture.cs             # Boots the Aspire AppHost; exposes ApiClient, KafkaBootstrapServers, FrontendUrl
├── ApiSmokeTests.cs             # Lightweight API reachability checks (health, alive, OpenAPI)
├── FrontendSmokeTests.cs        # Playwright tests against the Vue dashboard (logged-in context)
├── IssueKafkaNotificationTests.cs # Kafka message assertions after issue creation
└── Pages/                       # Page Object Model classes for Playwright
    ├── LoginPage.cs
    ├── DashboardPage.cs
    ├── OrgsPage.cs
    ├── OrgDetailPage.cs
    ├── IssuesPage.cs
    └── MilestonesPage.cs
```

New test files must be named after the feature domain they cover, e.g. `IssueCreationTests.cs`, `ProjectManagementTests.cs`. Do **not** name test files or classes after the test quality level (e.g. `HappyPathTests.cs` is an anti-pattern — it says nothing about what is being tested).

---

## Shared Fixture (`AspireFixture`)

All test classes belong to the `"E2E"` xUnit collection, which shares a single `AspireFixture` instance across the entire run. The fixture:

- Starts the Aspire AppHost once (Postgres, Kafka, Redis, API, frontend npm dev server).
- Exposes `ApiClient` (pre-configured `HttpClient` pointed at the API).
- Exposes `KafkaBootstrapServers` (resolved from the Aspire Kafka container).
- Exposes `FrontendUrl` (Aspire-started frontend URL, or falls back to `FRONTEND_URL` env var).

**Do not** start or stop the Aspire stack inside individual test classes.

```csharp
[Collection("E2E")]
[Trait("Category", "E2E")]
public class MyTests(AspireFixture fixture) { ... }
```

---

## Test Class Patterns

### API Tests

- Inject `AspireFixture` via primary constructor.
- Use `CreateCookieClient()` to get an `HttpClient` with a `CookieContainer` so session cookies persist across requests.
- Always call `GetDefaultTenantIdAsync()` and set `X-Tenant-Id` before any domain API call.
- Register a fresh user per test with a `Guid`-suffixed username to avoid cross-test collisions.

```csharp
private HttpClient CreateCookieClient()
{
    var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
    return new HttpClient(handler) { BaseAddress = fixture.ApiClient!.BaseAddress };
}

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

### UI (Playwright) Tests

- Implement `IAsyncLifetime` to create/dispose the Playwright browser per test class.
- Launch Chromium in headless mode with `Channel = "chrome"`.
- Read `FrontendUrl` from `_fixture.FrontendUrl ?? Environment.GetEnvironmentVariable("FRONTEND_URL")`.
- Create a new `IBrowserContext` per test (or per test class when a shared logged-in context is appropriate).
- Always use `Page Object Model` classes from the `Pages/` folder — do not write raw Playwright locators in test methods.

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

---

## Page Object Model

Each page in the Vue frontend has a corresponding class in `Pages/`. Page objects:

- Accept `IPage` in the primary constructor.
- Expose a `GotoAsync()` method that navigates and waits for the page to settle (`NetworkIdle`, heading selector, etc.).
- Expose action methods (e.g. `CreateIssueAsync`, `LoginAsync`) that encapsulate UI interactions.
- Expose `ILocator` properties for assertions in test methods — locators are defined using stable selectors (text content, `href`, `autocomplete`, `placeholder`, `type`).

Add a new page object class whenever a test needs to interact with a page not yet covered. One class per file.

---

## Naming Conventions

### What is a happy path test?

A **happy path test** is a positive end-to-end test that exercises the full successful flow of a feature — valid input, no errors, every step completes as expected. It is the most important test for any feature because it proves the core use case works end-to-end against the real stack.

Examples of what a happy path test covers for issue creation:
- Register a user, log in, create an org, create a project, create an issue
- Assert the issue appears in the list with the correct title
- Assert any side-effects (e.g. a Kafka event is published)

### Naming rules

Name test **files and classes** after the feature domain — never after the quality level:

```
// Good — class describes what is being tested
IssueCreationTests.cs
ProjectManagementTests.cs
MilestoneTests.cs

// Bad — class says nothing about what is tested
HappyPathTests.cs       ← anti-pattern
PositiveTests.cs        ← anti-pattern
```

Name test **methods** with the quality level and action:

| Type | Pattern | Example |
|------|---------|---------|
| API positive full-flow | `Api_HappyPath_<Action>` | `Api_HappyPath_CreateIssue` |
| UI positive full-flow | `Ui_HappyPath_<Action>` | `Ui_HappyPath_CreateMilestone` |
| Smoke / infra check | `Api_<Resource>Endpoint_<Expectation>` | `Api_HealthEndpoint_ReturnsOk` |
| Kafka / event check | `<Action>_Publishes<Event>_<Expectation>` | `CreateIssue_PublishesKafkaNotification_WithCorrectPayload` |
| Resource slugs/usernames | `Guid`-based, truncated to avoid DB length limits | |

---

## Kafka Tests

- Start consuming **before** creating the triggering entity to avoid the race where a message is produced before the consumer has received its partition assignment.
- Use `AutoOffsetReset.Latest` and a unique `GroupId` per test run (`Guid`-based).
- Suppress librdkafka noise with `.SetLogHandler((_, _) => { })`.
- Wait for `consumer.Assignment.Count > 0` before producing.
- Poll with a timeout (`CancellationTokenSource`) and match messages by key (`issueId`).
- Handle transient `UnknownTopicOrPart` errors during early polls.
- Call `consumer.Close()` before asserting.

---

## Best Practices

- **Isolation**: each test registers its own user and creates its own org/project/issue with unique slugs. Never share state between tests.
- **Helpers**: place `CreateCookieClient()` and `GetDefaultTenantIdAsync()` in the test class as private methods. If a second test class needs the same helpers, duplicate them — do not create a shared base class unless the duplication becomes significant.
- **Assertions**: assert HTTP status codes on every mutating call, not only on the final read.
- **Timeouts**: use the Playwright default timeouts; do not increase `WaitForURLAsync` / `WaitForSelectorAsync` timeouts beyond what is already established without investigating the root cause first.
- **Frontend URL**: if `FrontendUrl` is null in a UI test, throw `InvalidOperationException` with a clear message — do not fall back to a hardcoded `localhost` port.
- **Missing prerequisites = failure, not skip**: never use `SkipException` (or `return`) to silently skip a test when a required environment variable, service, or runtime is absent. Throw `InvalidOperationException` (or `Assert.Fail`) so the test is recorded as a **failure** and the missing setup is clearly visible in CI. Silently passing tests hide coverage gaps and make CI misleading.
- **Log noise**: resource logging is disabled in `AspireFixture` and Kafka log handlers are suppressed. Do not re-enable them in tests.
- **New features**: every new UI feature must include at least one positive E2E test covering create, list, and interact flows.
- **Never call `page.ReloadAsync()`**: this is an SPA — a full browser reload breaks the Vue router state and circumvents client-side navigation. To re-visit a page after resetting state (e.g. clearing localStorage), use the page object's `GotoAsync()` instead. This triggers a proper Playwright navigation that the Vue app handles correctly.
