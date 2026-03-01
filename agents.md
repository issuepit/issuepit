# IssuePit Agent Guidelines

## GitHub SSO & Token Management

### Overview

IssuePit supports GitHub Single Sign-On (OAuth 2.0) for authentication in both hosted and local
development environments.  When a user authenticates via GitHub, IssuePit:

1. Creates (or links) a local `User` record for the current tenant.
2. Stores the GitHub OAuth access token **encrypted at rest** using ASP.NET Core Data Protection
   in the `github_identities` table.
3. Issues a session cookie (`issuepit-session`) that identifies the user on subsequent requests.

### OAuth Flow

```
Browser / Agent
  │
  ▼
GET /api/auth/github[?returnUrl=/]
  │  (backend redirects to GitHub)
  ▼
https://github.com/login/oauth/authorize
  │  (user grants access)
  ▼
GET /api/auth/github/callback?code=…&state=…
  │  (backend exchanges code → token, upserts user+identity, sets cookie)
  ▼
Redirect → frontend (returnUrl)
```

Requested scopes: `read:user user:email repo` (read/write repository access).

### Configuration

Add the following to `appsettings.Development.json` (or environment variables in production):

```json
{
  "GitHub": {
    "OAuth": {
      "ClientId": "<your GitHub OAuth App client ID>",
      "ClientSecret": "<your GitHub OAuth App client secret>",
      "CallbackUrl": "http://localhost:5000/api/auth/github/callback",
      "FrontendUrl": "http://localhost:3000"
    }
  }
}
```

Create a GitHub OAuth App at **Settings → Developer Settings → OAuth Apps** with:
- **Homepage URL**: `http://localhost:3000` (or your hosted domain)
- **Authorization callback URL**: `http://localhost:5000/api/auth/github/callback`

### Token Retrieval for Agent Integrations

Agents (GitHub CLI, Copilot, OpenCode, etc.) that need a GitHub token can retrieve it from the
authenticated session:

```
GET /api/auth/token
Cookie: issuepit-session=<value>
```

Response:
```json
{
  "token": "<GitHub OAuth access token>",
  "githubUsername": "octocat"
}
```

Use the token with the GitHub CLI:
```bash
export GITHUB_TOKEN=$(curl -s -b "issuepit-session=..." http://localhost:5000/api/auth/token | jq -r .token)
gh auth login --with-token <<< "$GITHUB_TOKEN"
```

Or configure it directly as `GH_TOKEN` / `GITHUB_TOKEN` in agent environments.

### Identity Linking

A single local user can be linked to **multiple GitHub identities** (one per tenant/project
context).  Each `GitHubIdentity` record stores:

| Field | Description |
|-------|-------------|
| `GitHubId` | GitHub's stable numeric user ID |
| `GitHubUsername` | Current GitHub login handle |
| `GitHubEmail` | Primary verified email from GitHub |
| `EncryptedToken` | AES-256 encrypted OAuth token (Data Protection) |

The token is refreshed (re-encrypted) automatically on every successful login.

---

## Testing and Coding

- Create Aspire integration tests
- AI agents must execute tests before ending a task/session
- E2E tests for Vue use the Aspire backend
- All PRs run all actions for frontend and backend
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

