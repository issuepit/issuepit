---
title: CI/CD Integration
layout: default
nav_order: 7
---

# CI/CD Integration

IssuePit can run GitHub Actions workflows locally using [nektos/act](https://github.com/nektos/act), without pushing to GitHub. This lets you trigger CI/CD pipelines directly from IssuePit and view the results alongside your issues.

---

## How it works

1. IssuePit watches your linked Git repository for new commits.
2. When a commit arrives (or you trigger a run manually), `IssuePit.CiCdClient` runs the repository's `.github/workflows/` files locally using `act`.
3. Job status, logs, and artifacts are streamed back to the IssuePit UI in real time.

---

## Setting Up CI/CD

### 1. Link a Git repository to your project

See [Projects → Linking a Git Repository](projects#linking-a-git-repository).

### 2. Configure the runner image

1. Open your project.
2. Go to **Project → Settings → CI/CD**.
3. Under **Runner Image**, choose the Docker image that `act` will use to execute workflow jobs.

The default image is `ghcr.io/issuepit/helper-act:latest` which includes Docker CLI, `act`, and `actionlint`. You can also configure a default at the organisation level under **Organisation → Settings → CI/CD**.

### 3. Trigger a run

CI/CD runs start automatically when new commits are detected. You can also trigger one manually:

1. Go to **Project → CI/CD**.
2. Click **Trigger Run**.
3. Select the branch or commit and the event type (e.g. `push`, `workflow_dispatch`).
4. Click **Run**.

---

## Viewing Run Results

Navigate to **Project → Runs** (or the global **Runs** page in the sidebar) to see all CI/CD runs. Click any run to open the detail view showing:

- **Job graph** — a visual DAG of all jobs with status indicators (queued, running, success, failure)
- **Logs** — real-time streaming logs per step, with search support
- **Artifacts** — downloadable files produced by workflow steps
- **Matrix jobs** — expanded matrix strategy runs displayed individually

> **Tip:** Use the **Slim mode** toggle (top-right of the run page) to collapse the job graph and focus on the log output.

---

## Concurrent Job Limits

By default, IssuePit limits CI/CD to **4 concurrent jobs** per organisation. To change this:

1. Go to **Organisation → Settings → CI/CD**.
2. Update **Max Concurrent Jobs**.

You can also override the limit per project under **Project → Settings → CI/CD**.

---

## Caching

IssuePit provides persistent package caching to speed up builds:

- **npm packages** — `node_modules` are cached between runs
- **NuGet packages** — .NET restore packages are cached between runs

Caching is enabled by default when running via the `helper-act` image and requires no extra configuration.

---

## Action Cache & Offline Mode

act downloads GitHub Actions from the marketplace on first use. IssuePit can cache them locally so subsequent runs do not require internet access. You can also reroute `actions/checkout` and similar actions to a local mirror.

Configure via environment variables in your `docker-compose.yml`:

| Variable | Description |
|----------|-------------|
| `CiCd__ActionCachePath` | Host path for the local act action cache |
| `CiCd__ActImage` | Default runner image passed to act via `-P` flags |

---

## Secrets and Environment Variables

Pass workflow secrets and environment variables to a CI/CD run:

1. Open a CI/CD run detail page.
2. Click **Advanced Options** (or use the options when triggering a new run).
3. Add key–value pairs under **Secrets** and **Environment Variables**.

Secrets are injected into `act` as `--secret` arguments and are never stored persistently.

---

## Helper Containers

IssuePit ships pre-built helper container images for CI/CD agent use:

| Image | Contents |
|-------|---------|
| `ghcr.io/issuepit/helper-act` | Docker CLI + `act` + `actionlint` |
| `ghcr.io/issuepit/helper-opencode-act` | `helper-act` + `opencode` for combined agent/CI/CD runs |

See [Helper Containers →](developer/helper-containers) for details.

---

## Test History

Every CI/CD run that produces a `.trx` artifact automatically has its test results stored in the IssuePit database. The **Test History** page (`Project → Runs → Test History`) lets you analyse this data over time.

See [Projects → Test History](projects#test-history) for full documentation.

---

## Next Steps

- [Projects → Runs](projects#runs)
- [Helper Containers →](developer/helper-containers)
- [Configuration →](configuration)
