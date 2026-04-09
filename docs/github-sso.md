---
title: GitHub SSO
---

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

Agents (GitHub Copilot CLI, OpenCode, etc.) that need a GitHub token can retrieve it from the
authenticated session. This is done by `IssuePit.ExecutionClient` on startup of an agent session
together with other environment variables (e.g., `GITHUB_USERNAME`) and keys.:

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
