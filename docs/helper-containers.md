---
title: Helper Containers
layout: default
nav_order: 8
---

# Helper Containers

IssuePit ships pre-built Docker images that provide a consistent, batteries-included environment for agent runs and CI/CD pipelines. All images are published to the GitHub Container Registry (GHCR) under the `ghcr.io/issuepit/` namespace and versioned independently from the main application release.

---

## Images

### `issuepit-helper-base`

A shared base image that all other helper images extend.

**Includes:**
- [.NET SDK](https://dot.net) (via `mcr.microsoft.com/playwright/dotnet`)
- [Playwright](https://playwright.dev) + Chrome/Chromium
- [Node.js](https://nodejs.org) / npm (NodeSource LTS)

**Registry:** `ghcr.io/issuepit/issuepit-helper-base`

---

### `issuepit-helper-act`

Extends `helper-base` with [nektos/act](https://github.com/nektos/act), which lets you run GitHub Actions workflows locally inside the container — used by `IssuePit.CiCdClient` for local CI runs.

**Includes:** everything in `helper-base` + `act`

**Registry:** `ghcr.io/issuepit/issuepit-helper-act`

---

### `issuepit-helper-opencode`

Extends `helper-base` with the [opencode CLI](https://github.com/anomalyco/opencode), an AI-powered coding agent — used by `IssuePit.ExecutionClient` for agent runs.

**Includes:** everything in `helper-base` + `opencode-ai` (npm global)

**Registry:** `ghcr.io/issuepit/issuepit-helper-opencode`

---

## Image Tags

Tags follow the pattern `<version>-dotnet<DOTNET_MAJOR>-node<NODE_MAJOR>`, making the bundled runtime versions explicit:

| Tag | Description |
|-----|-------------|
| `latest` | Most recent release build |
| `dotnet10-node24` | Floating tag for dotnet 10 + Node 24 builds |
| `1.0.0-dotnet10-node24` | Pinned release with exact runtime versions |
| `1.0-dotnet10-node24` | Pinned minor release |
| `sha-<short-sha>` | Specific commit build |
| `main-dotnet10-node24` | Latest commit on `main` |

---

## Build Configuration

The Dockerfiles accept build arguments that you can override when building locally:

| Argument | Image | Default | Description |
|----------|-------|---------|-------------|
| `PLAYWRIGHT_VERSION` | `helper-base` | `v1.50.1` | Playwright .NET image tag (e.g. `v1.51.0`) |
| `NODE_MAJOR` | `helper-base` | `24` | Node.js major version |
| `BASE_IMAGE` | `helper-act`, `helper-opencode` | `ghcr.io/issuepit/issuepit-helper-base:latest` | Base image reference |
| `ACT_VERSION` | `helper-act` | `0.2.74` | nektos/act release version |
| `OPENCODE_VERSION` | `helper-opencode` | `latest` | opencode-ai npm package version |

### Building locally

```bash
# Build the base image
docker build \
  --build-arg PLAYWRIGHT_VERSION=v1.50.1 \
  --build-arg NODE_MAJOR=24 \
  -f docker/Dockerfile.helper-base \
  -t issuepit-helper-base:local \
  .

# Build the act image from the local base
docker build \
  --build-arg BASE_IMAGE=issuepit-helper-base:local \
  --build-arg ACT_VERSION=0.2.74 \
  -f docker/Dockerfile.helper-act \
  -t issuepit-helper-act:local \
  .

# Build the opencode image from the local base
docker build \
  --build-arg BASE_IMAGE=issuepit-helper-base:local \
  -f docker/Dockerfile.helper-opencode \
  -t issuepit-helper-opencode:local \
  .
```

---

## Release Cycle

Helper containers use their own independent release-please cycle, separate from the main IssuePit application release.

- Release PR bot tracks changes under `docker/helper-containers/`
- Tags follow `helper-containers-v<semver>` (e.g. `helper-containers-v1.0.0`)
- The build workflow (`.github/workflows/helper-containers.yml`) triggers automatically on those tags and on `docker/Dockerfile.helper-*` changes pushed to `main`

To publish a new release, bump the version in `docker/helper-containers/version.txt` — release-please will open a PR and tag the release automatically once merged.

---

## Updating Versions

To upgrade bundled runtimes:

1. Update the `env` constants in `.github/workflows/helper-containers.yml`:
   ```yaml
   DOTNET_MAJOR: "10"
   NODE_MAJOR: "24"
   PLAYWRIGHT_VERSION: "v1.51.0"
   ACT_VERSION: "0.2.74"
   ```
2. Update the `ARG` defaults in `docker/Dockerfile.helper-base` (and other Dockerfiles) to match.
3. Bump `docker/helper-containers/version.txt` so release-please creates a new release.
