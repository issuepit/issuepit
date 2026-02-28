# IssuePit Agent Guidelines

## Testing and Coding

- Create Aspire integration tests
- AI agents must execute tests before ending a task/session
- E2E tests for Vue use the Aspire backend
- All PRs run all actions for frontend and backend
- **PRs that include UI changes must include screenshots of the UI in the PR description**
- Keep changes minimal; create commits after each task/work piece; don't do refactors
- Check that helpers and other methods were not created twice
- Do not remove comments
- Do not create MD files for task descriptions done in this session

- Always run tests at the end of a session
- Use conventional commits
- Commit after each small logical change (a session can have multiple commits)
- Keep changes minimal and focused
- Do not create MD files describing your task if not asked to
- Do not create tests without value (e.g., simple property getters, string comparisons, basic configuration checks)
- Prefer strict types (enums, strongly-typed classes) over strings and primitives where appropriate
- One class per file (except for small private nested classes used only within the parent class)
- EF Core properties should be defined as attributes and not in `DbContext.OnModelCreating`
- **Preserve commented-out code that provides context** (e.g., old implementations, alternative approaches, protocol examples) — such code serves as documentation and should not be removed during refactoring

## Git Usage

- Create commits after each step/task
- Commit messages must follow [Conventional Commits](https://www.conventionalcommits.org/) style

## Documentation

- Do not add redundant information in documentation (e.g., listing what tests cover when the tests themselves are self-documenting)
- Keep documentation focused on concepts, formats, and implementation approaches rather than test descriptions

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
