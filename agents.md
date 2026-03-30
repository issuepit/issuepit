this file descripes rules on how agenting coding tools work with this repository

# IssuePit Agent Guidelines

## Agent Terminology

- **Agent mode** — a configuration with a system prompt and settings such as MCP tools and authentication keys. This is what you create in the IssuePit UI.
- **(Work) agent** — the CLI tool or process (e.g. `opencode`, `codex`, GitHub Copilot CLI) that performs the actual work. It is launched with an agent mode configuration.
- **Agent** — can refer to either concept, but typically means the entity doing work. An agent mode is executed _by_ a work agent.
- Multiple agent modes can be executed by multiple work agents in parallel.
- Work agents are spawned by `IssuePit.ExecutionClient` with the agent mode configuration on demand whenever work needs to be done.

## Testing and Coding

- Create Aspire integration tests
- AI agents must execute tests before ending a task/session
- E2E tests for Vue use the Aspire backend
- All PRs run all actions for frontend and backend
- **The Aspire stack runs in the sandboxed agent environment** — `copilot-setup-steps.yml` pre-installs all required infrastructure (Docker images, frontend npm dependencies, Playwright). Always run E2E tests at the end of a session using:
  ```sh
  dotnet test src/IssuePit.Tests.E2E/IssuePit.Tests.E2E.csproj \
    --configuration Release \
    --filter "Category=E2E|Category=Smoke|Category=CiCd|Category=Agent" \
    --verbosity normal --blame-hang-timeout 4min
  ```
  Run the command **twice** and report both total runtimes to confirm stability.
  > **Note:** The GitHub-hosted runner sets `DOTNET_SYSTEM_NET_DISABLEIPV6=1`, which prevents the .NET Aspire client from reaching the DCP gRPC endpoint on `[::1]`. The `copilot-setup-steps.yml` re-enables IPv6 and sets `CICD_E2E_HELPER_ACT_IMAGE` via `GITHUB_ENV` so Aspire and Docker-runtime CI/CD tests start correctly. If you ever see `Polly.Timeout.TimeoutRejectedException` when starting the Aspire fixture, verify that `DOTNET_SYSTEM_NET_DISABLEIPV6` is `0` or unset.
- Keep changes minimal; create commits after each task/work piece; don't do refactors
- Check that helpers and other methods were not created twice
- Do not remove comments
- Do not create MD files for task descriptions done in this session
- **PRs that include UI changes must add screenshots** showing the new/changed UI in the PR description or as a PR comment
- **New UI features must include basic positive E2E tests** covering the main flow (create, list, interact)
- **E2E tests must use Page Object Model (POM)** — create or extend a page object in `src/IssuePit.Tests.E2E/Pages/` for each page under test; tests should interact with pages only through these page objects and avoid inline Playwright calls
- **Do not increase CI timeouts** as a workaround — if a job exceeds its timeout something is structurally wrong (wrong filters, missing pre-conditions, etc.). Investigate and fix the root cause. Flakiness may justify a small margin but long timeouts are not preferred.

- Always run tests at the end of a session
- Use conventional commits
- Commit after each small logical change (a session can have multiple commits)
- Keep changes minimal and focused
- Do not create MD files describing your task if not asked to
- Do not create tests without value (e.g., simple property getters, string comparisons, basic configuration checks)
- **Always run `npm run lint` (or `node_modules/.bin/eslint .`) on the frontend before closing a session** — fix all lint *errors* (unused vars, type-only imports, etc.); warnings from pre-existing code do not need to be resolved
- Prefer strict types (enums, strongly-typed classes) over strings and primitives where appropriate
- One class per file (except for small private nested classes used only within the parent class)
- EF Core properties should be defined as attributes and not in `DbContext.OnModelCreating`
- **Preserve commented-out code that provides context** (e.g., old implementations, alternative approaches, protocol examples) — such code serves as documentation and should not be removed during refactoring

## Git Usage

- Create commits after each step/task
- **Commits MUST be made after each complete task/step in an implementation session** — do not batch all changes into a single commit at the end
- Commit messages must follow [Conventional Commits](https://www.conventionalcommits.org/) style

# Agent Workflow

## PR Handling
- if UI was changed/related add screenshots after each session as PR comment; if important or final add to main PR content too
  - use proper markdown image embedding
  - check if images actually show what we expect them to show (no blank pages, wrong position of elements, wrong colors, dark mode not working, ...)
  - evaluate if url pattern is correct:
    - wrong: https://github.com/user-attachments/assets/ribbon1-classic.png
    - good: https://github.com/user-attachments/assets/7091a86c-8cbf-4970-bccd-1c3fee4780fe
    - good: https://private-user-images.githubusercontent.com/122795841/518943158-75803498-1b92-4fce-9b8e-75e68400169c.png?jwt=...token... # gets replaced from former link
- PR title should follow conventional commit style
- includes a list of what tasks had to be done and check which are already done
- branches should contain the PR/issue number like `copilot/123-fix-of-xyz`

## Ending a Task / Conversation / Session

**Definition of Done**: A session is only complete when:

- All code changes can be executed without errors
- All tests run green (no exceptions — all necessary tools are available for running e2e tests or database tests)
- Build succeeds with no breaking errors
- Code has been validated through testing

At the end, scan similar files and evaluate if there is similar/duplicated code:
- Refactor **only** if it is safe and minimal
- Inform the user about it
- Create a separate commit for each refactor (so review is easy)

# Documentation

- Do not add redundant information in documentation (e.g., listing what tests cover when the tests themselves are self-documenting)
- Keep documentation focused on concepts, formats, and implementation approaches rather than test descriptions
- **When adding a new user-facing feature, update `docs/` accordingly** — add or extend the relevant page (e.g. `docs/projects.md`, `docs/agents.md`, or create a new page). If applicable, add a screenshot entry to `scripts/take-screenshots.js` so the automated screenshot workflow covers the new page.
- For docs design conventions, see `docs/issuepitAgents.md`.

# Coding Agent Guidelines

When working as a coding agent on this repository, follow these conventions:

## Error Handling

- **Do not hide errors with silent fallbacks.** A fallback that masks a misconfiguration (e.g. cloning a git repo without `--branch` when the configured branch does not exist in the remote) prevents the user from understanding what went wrong. Instead, fail fast with a clear, actionable error message that identifies the misconfiguration and how to fix it.

- **No hidden fallbacks when configuration is incomplete or ambiguous.** This applies broadly: git remote selection, runtime resolution, credential lookup, and any other place where the system cannot determine the correct value unambiguously. When the required configuration is absent or incorrect, fail immediately with an error that tells the user exactly what to fix. Silent fallbacks let runs proceed in an unexpected state, making failures far harder to diagnose. Examples of what NOT to do: falling back to the first available remote when no Working remote is configured; falling back to a default org when the target org is missing; silently ignoring a missing credential and proceeding unauthenticated.

- **Never silently do things that were not explicitly requested.** Do not invent hidden triggers or surrogate inputs (e.g. stub issues, fallback project IDs) to bypass missing required data — instead, fail with a clear error.

## Date Formats

Always use **ISO 8601 format** (`YYYY-MM-DD`) for dates in custom issue properties, API responses, and any user-visible date fields. Do not rely on browser locale formatting (e.g. `mm/dd/yyyy`) for date values stored or displayed in the application. Date inputs in forms should accept and display dates in `YYYY-MM-DD` format.

For **displaying** dates and times in the Vue frontend, always use the shared `<DateDisplay>` component (`frontend/components/DateDisplay.vue`) instead of inline `toLocaleString`/`toLocaleDateString` calls:

```vue
<!-- Absolute date, European format: "16. Jan 2025" -->
<DateDisplay :date="item.createdAt" mode="absolute" resolution="date" />

<!-- Absolute datetime, 24h clock: "16. Jan 2025, 14:30" -->
<DateDisplay :date="item.startedAt" mode="absolute" resolution="datetime" />

<!-- Relative: "3 minutes ago", "2 hours ago", "yesterday" (tooltip shows full datetime) -->
<DateDisplay :date="item.updatedAt" mode="relative" />

<!-- Auto: relative for recent dates (<7d), absolute beyond that -->
<DateDisplay :date="item.startedAt" mode="auto" />
```

Key formatting rules enforced by `<DateDisplay>`:
- **24-hour clock** — never use AM/PM
- **European day-first format** — `16. Jan 2025` not `Jan 16, 2025`
- Relative labels: `just now`, `X minutes ago`, `X hours ago`, `yesterday`, `X days ago`

## API Response Objects

- **Always use named `record` or `class` types for API responses** — do not use anonymous objects (`new { ... }`) in controller actions.
  Named types improve discoverability, reusability, and compile-time safety.
  Declare response/request records at the bottom of the controller file (same namespace), following the pattern used throughout the codebase (e.g. `SetAgentActiveRequest`, `AgentResponse`, `AgentDetailResponse`, `LinkedMcpServerDto`, etc.).
  ```csharp
  // ✅ Correct
  return Ok(new AgentResponse(agent.Id, agent.Name, ...));

  // ❌ Avoid
  return Ok(new { agent.Id, agent.Name, ... });
  ```

## Testing Conventions

- **Tests must never be silently skipped to hide failures.** A test that returns without asserting (e.g. `if (condition) return;`) counts as a passing test even when the feature under test is completely broken. This masks real failures.
- If a test genuinely cannot run in a given environment, use `Skip.If` / `Skip.Unless` (or `Assert.Skip`) with an **explicit, human-readable reason** so the skip is visible in test results and CI logs.
- Prefer fixing the underlying precondition (e.g. downloading a required asset at test start) over skipping.

- **Always run the frontend linter before closing a session:**
  ```sh
  cd frontend && node_modules/.bin/eslint .
  ```
  Fix all lint **errors** (unused variables, type-only imports, etc.). Pre-existing warnings from unrelated code do not need to be resolved.

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

**Example:**

```csharp
// ✅ correct
context.SetDefaultTimeout(E2ETimeouts.Default);
await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });
await page.WaitForSelectorAsync("button:has-text('New Issue')", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });

// ❌ wrong – magic number, hard to tune globally
await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });
```

## PR Screenshots

Screenshots in PRs and documentation must show the actual UI **after authentication** — the login page is not useful. The `scripts/take-screenshots.js` script handles this by:

1. Logging in (or registering) a user before taking any screenshots.
2. Accepting a pre-seeded user via `SCREENSHOT_USERNAME` / `SCREENSHOT_PASSWORD` environment variables (the Aspire Migrator seeds `alice`/`alice` for the Acme Corp org).

To take screenshots manually during development:

```sh
# Start the full stack first (Aspire), then:
FRONTEND_URL=http://localhost:3000 \
API_URL=http://localhost:5000 \
SCREENSHOT_USERNAME=alice \
SCREENSHOT_PASSWORD=alice \
node scripts/take-screenshots.js /tmp/screenshots
```

Always verify that uploaded screenshots show the intended UI (not a blank page, wrong page, or the login screen).

## UI Conventions

### Delete Operations Must Show a Confirm Modal

**All destructive delete operations in the UI must show a confirmation modal** before executing.
Never call a delete API directly from a button click without first showing a modal that requires the user to confirm.

This applies to deleting: issues, attachments, agents, runtimes, MCP servers, API keys, labels, milestones, and any other entity.

The confirmation modal must:
- Clearly state what is being deleted (include the item name where possible).
- Warn that the action cannot be undone.
- Provide a prominent red **Delete** button and a neutral **Cancel** button.

### Searchable Multi-Select Inputs

**Use the `<MultiSelect>` component** (`frontend/components/MultiSelect.vue`) for any filter or selection field where:
- the user may want to select **multiple values**, or
- the list of options is long enough to benefit from a **search/filter input** (e.g. branches, agents, usernames, labels, statuses).

Never use a plain `<input type="text">` for a filter that maps to a discrete set of options. Examples that must use `<MultiSelect>`:
- Branch filters (test history, run lists, CI/CD views)
- Agent assignment filters
- Username / member filters
- Label and status filters

```vue
<MultiSelect
  v-model="selectedBranches"
  :options="branchOptions"
  placeholder="All Branches"
/>
```

Where `options` is `MultiSelectOption[]` (`{ value: string, label: string, dotClass?: string }`).
Populate `options` from the appropriate API endpoint so the user sees real values.
The component handles search, keyboard navigation, checkbox selection, and outside-click dismissal.

