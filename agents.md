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

