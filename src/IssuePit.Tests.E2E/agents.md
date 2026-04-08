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
- **Stable selectors over retry logic**: prefer selectors that can only match when the page is in the correct state (e.g. waiting for a data-loaded element that only renders after API calls complete) rather than wrapping clicks in try/catch retry loops. Retry logic masks the root cause and makes tests harder to debug. Use the established retry pattern (`Short` → `Task.Delay(RetryDelay)` → re-click → `Navigation` timeout) **only** as a last resort for known Vue SSR hydration races where the element is visible but the click handler has not yet been attached — and document why.
- **Frontend URL**: if `FrontendUrl` is null in a UI test, throw `InvalidOperationException` with a clear message — do not fall back to a hardcoded `localhost` port.
- **Missing prerequisites = failure, not skip**: never use `SkipException` (or `return`) to silently skip a test when a required environment variable, service, or runtime is absent. Throw `InvalidOperationException` (or `Assert.Fail`) so the test is recorded as a **failure** and the missing setup is clearly visible in CI. Silently passing tests hide coverage gaps and make CI misleading. This applies equally to Aspire-hosted services (e.g. `GitServerUrl`, `FrontendUrl`, docker, act): if the service is expected to be running, its absence is a configuration error that must surface as a test failure.
- **Log noise**: resource logging is disabled in `AspireFixture` and Kafka log handlers are suppressed. Do not re-enable them in tests.
- **New features**: every new UI feature must include at least one positive E2E test covering create, list, and interact flows.
- **Never call `page.ReloadAsync()`**: this is an SPA — a full browser reload breaks the Vue router state and circumvents client-side navigation. To re-visit a page after resetting state (e.g. clearing localStorage), use the page object's `GotoAsync()` instead. This triggers a proper Playwright navigation that the Vue app handles correctly.

---

## Agent Orchestration — Transition Requirements

When implementing or modifying kanban transition requirement checks, follow these rules:

### `RequireGreenCiCd`

- **Always check by `issue.GitBranch`** — no fallback.
- If the issue has no `GitBranch` set, the requirement is **not met** and the transition is **blocked** with the reason `"No git branch set on the issue — CI/CD requirement cannot be evaluated."`.
- Never fall back to `AgentSession.IssueId` or project-scope CI/CD runs. Using a fallback would hide the real misconfiguration (the issue not having a branch) and allow the transition to proceed in an unexpected state.

### General principle

> **No hidden fallbacks when configuration is incomplete.** If a required piece of configuration (e.g. `GitBranch`) is missing, fail fast with a clear, actionable error message. Do not silently use an alternative source that produces a less precise result.

This applies to all transition requirements. Any new requirement added in the future must:
1. Clearly document what configuration it requires.
2. Return a specific error message identifying the missing configuration when it cannot be evaluated.
3. Never silently pass or fall back to an approximation.


## E2E Playwright Timeout Conventions

All Playwright timeout values in E2E tests and page objects **must** use the named constants from
`src/IssuePit.Tests.E2E/E2ETimeouts.cs`. Never use magic numbers like `10_000` directly.

| Constant | Value | When to use |
|---|---|---|
| `E2ETimeouts.Short` | 5 s | First-attempt / hydration-retry check; brief UI-feedback waits (modal opened, voice recording started). A second attempt with `Default` will follow on failure. |
| `E2ETimeouts.Default` | 10 s | General element presence/interaction wait; value passed to `SetDefaultTimeout`. |
| `E2ETimeouts.Navigation` | 15 s | Full-page navigations (post-login redirect, initial project-page load). |
| `E2ETimeouts.NavigationLong` | 20 s | Slower cross-page navigations using `WaitUntilState.Commit` (e.g. org or agent detail pages). |
| `E2ETimeouts.RetryDelay` | 1.5 s | `Task.Delay` between a failed navigation attempt and its retry. Not a Playwright timeout. |
| `E2ETimeouts.LogPollTimeoutMs` | 30 s | Deadline for polling the session log API until an expected log line appears. Not a Playwright timeout. |
| `E2ETimeouts.LogPollDelayMs` | 500 ms | `Task.Delay` between successive session-log poll attempts. Not a Playwright timeout. |

**Example:**

```csharp
// ✅ correct
context.SetDefaultTimeout(E2ETimeouts.Default);
await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });
await page.WaitForSelectorAsync("button:has-text('New Issue')", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });

// ❌ wrong – magic number, hard to tune globally
await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });
```