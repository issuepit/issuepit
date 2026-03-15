---
title: Config Repo (Infrastructure as Code)
layout: default
nav_order: 8
---

# Config Repo — Infrastructure as Code

IssuePit supports managing organisation and project settings via a **config repository** — a Git repository (or local directory) that holds JSON5 config files. On startup and every 5 minutes, IssuePit fetches the repo and applies any changes to the database automatically.

This lets you version-control your entire IssuePit configuration alongside your code.

---

## Setting Up a Config Repo

1. Go to **Admin → Tenants**.
2. Click **Config Repo** next to the tenant you want to configure.
3. Fill in:

   | Field | Description |
   |-------|-------------|
   | **URL** | Git URL (`https://…`, `git@…`) or a local filesystem path |
   | **Token** | PAT or access token for authenticating with the Git remote |
   | **Username** | Git username for HTTP basic auth (defaults to `git`) |
   | **Strict mode** | Controls unknown user resolution in member lists. When enabled, unknown users are logged as warnings and skipped but all other config fields in the file are still applied; when disabled, unknown users are silently skipped. Does not affect entity slug matching. |

4. Click **Save**.
5. Click **Sync Now** to run an immediate sync. The result shows the number of files processed and any warnings or validation errors.

You can also set these values via environment variables (useful for startup injection in Docker Compose or Kubernetes):

```env
ConfigRepo__Url=https://github.com/my-org/config-repo.git
ConfigRepo__Token=ghp_xxxx
ConfigRepo__Username=git
ConfigRepo__StrictMode=false
```

---

## Config Directory Structure

The config repo must have the following directory layout:

```
config-repo/
  orgs/
    my-org.json5          ← organisation overrides (slug = file name or "slug" field)
  projects/
    my-project.json5      ← project overrides
    my-advanced.json5
```

Both `.json5` and `.json` files are accepted. JSON5 is recommended because it supports **inline comments** and **trailing commas**.

Only the fields you set are applied — omitted fields are left unchanged in the database.

---

## Organisation Config (`orgs/*.json5`)

```json5
{
  // slug defaults to the file name (without extension) when omitted
  "slug": "my-org",
  "name": "My Organisation",

  // Runner settings
  "maxConcurrentRunners": 4,    // 0 = unlimited
  "concurrentJobs": 2,          // null = use system default
  "actRunnerImage": "catthehacker/ubuntu:act-22.04",

  // Environment variables for act (one KEY=VALUE per line, use \n as separator in JSON strings)
  "actEnv": "DOCKER_HOST=unix:///var/run/docker.sock\nNODE_ENV=production",

  // Secrets for act (one KEY=VALUE per line)
  "actSecrets": "NPM_TOKEN=replace_me\nDOCKER_PASSWORD=replace_me",

  // Action cache
  "actionCachePath": "/home/runner/.cache/act",
  "useNewActionCache": true,
  "actionOfflineMode": false,

  // Members — resolved by userId (GUID, priority) or username
  "members": [
    { "username": "alice", "role": "owner" },
    { "username": "bob",   "role": "admin" },
    { "userId": "00000000-0000-0000-0000-000000000001", "role": "member" },
  ],
}
```

### Organisation member roles

| Role | Description |
|------|-------------|
| `owner` | Full control including deleting the organisation |
| `admin` | Manage members, projects, and settings |
| `member` | Access to projects as permitted by project settings |

---

## Project Config (`projects/*.json5`)

### Simple project — single git origin

```json5
{
  "slug": "my-project",
  "orgSlug": "my-org",   // must match an existing organisation slug
  "name": "My Project",
  "description": "A private GitHub project managed via config.",

  // Single git origin shorthand
  "gitUrl": "https://github.com/my-org/my-repo.git",
  "gitToken": "ghp_REPLACE_ME",   // PAT with repo:read + contents:write
  "defaultBranch": "main",

  // Runner overrides (inherit from org when omitted)
  "maxConcurrentRunners": 2,
  "mountRepositoryInDocker": true,

  "members": [
    { "username": "alice", "permissions": 127 },
  ],
}
```

### Advanced project — multiple git origins

```json5
{
  "slug": "my-advanced-project",
  "orgSlug": "my-org",

  // Multiple git origins (overrides the single-origin "gitUrl" shorthand).
  // Origins are matched by remoteUrl; DB origins not listed here are left unchanged.
  "gitRepos": [
    {
      // Working origin — agents push branches and open PRs here
      "remoteUrl": "https://github.com/my-org/my-repo.git",
      "gitToken": "ghp_WORKING_TOKEN",
      "defaultBranch": "main",
      "mode": "Working",   // Working | Release | ReadOnly
    },
    {
      // Release origin — release pipeline pushes the default branch here
      "remoteUrl": "https://github.com/my-org/my-repo-releases.git",
      "gitToken": "ghp_RELEASE_TOKEN",
      "defaultBranch": "main",
      "mode": "Release",
    },
  ],

  // Redirect reusable workflow calls to local clones.
  // Format: owner/repo=localPath  (multiple entries separated by \n in the JSON string)
  "localRepositories": "my-org/reusable-workflows=/home/runner/local/reusable-workflows\nmy-org/shared-actions=/home/runner/local/shared-actions",

  "actionCachePath": "/home/runner/.cache/act",
  "useNewActionCache": true,
  "actRunnerImage": "catthehacker/ubuntu:act-22.04",
}
```

### Git origin modes

| Mode | Description |
|------|-------------|
| `Working` | Primary remote. Agents push feature branches here and open PRs. |
| `Release` | Release pipeline pushes the default branch here after merge. |
| `ReadOnly` | Fetch-only mirror — never pushed to by agents or the pipeline. |

### Project member permissions

Permissions are stored as a bitmask integer. Common values:

| Value | Meaning |
|-------|---------|
| `1` | Read issues |
| `3` | Read + create issues |
| `67` | Read + create + manage issues |
| `127` | Full project access |

---

## Schema Validation

All config files are validated against a schema after parsing. If a field fails validation (e.g. `maxConcurrentRunners` exceeds the allowed range), the **entire file is skipped** and the error appears in the sync result shown in the UI.

Validation rules include:

| Field | Rule |
|-------|------|
| `maxConcurrentRunners` | 0–1000 |
| `concurrentJobs` | 0–100 |
| `remoteUrl` | Required (non-empty) |
| String fields | Maximum length enforced |

---

## Sync Behaviour

- On startup and every **5 minutes**, IssuePit syncs the configured config repo.
- You can also trigger an on-demand sync via **Admin → Tenants → Config Repo → Sync Now**.
- Files are processed in order: `orgs/` first, then `projects/`.
- If a slug does not match any existing entity, the file is **skipped with a warning** (entities are never created automatically from config). This applies regardless of strict mode — strict mode only affects member resolution.
- When `orgSlug` is set but the org is not found, the project file is **skipped** entirely.
- Only non-null fields are applied — unset fields keep their current DB value.
- Malformed JSON5 files are skipped with an error; other valid files in the same sync are still applied.
- **Strict mode** only controls member resolution: unknown users in `members` lists are logged as warnings and skipped, but all other fields in the file (name, runner settings, git repos, etc.) are still applied.

---

## Example Config Directory

The repository includes `config-repo.example/` with ready-to-use JSON5 files:

```
config-repo.example/
  orgs/my-org.json5
  projects/my-private-project.json5
  projects/my-advanced-project.json5
```

To use them:

1. Copy or rename `config-repo.example/` to any directory (e.g. `config-repo/`).
2. Edit the files to match your slugs and replace placeholder tokens.
3. Set the path (or git remote URL) under **Admin → Tenants → Config Repo**.

> **Note:** `config-repo/` is in `.gitignore` — IssuePit auto-imports from this path when `ConfigRepo:Url` is set. Use `config-repo.example/` as the version-controlled template.

---

## Environment Variable Reference

| Variable | Description |
|----------|-------------|
| `ConfigRepo__Url` | Git URL or local path of the config repository |
| `ConfigRepo__Token` | Authentication token for Git HTTP access |
| `ConfigRepo__Username` | Git username (default: `git`) |
| `ConfigRepo__StrictMode` | `true` to log warnings for unknown users |

> **Note:** Environment variables use `__` (double underscore) as the separator. In `appsettings.json` or code the same settings use `:` (e.g. `ConfigRepo:Url`).
