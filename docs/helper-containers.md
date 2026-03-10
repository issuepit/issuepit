---
title: Helper Containers
layout: default
parent: Developer
nav_order: 2
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
- [s5cmd](https://github.com/peak/s5cmd) — FOSS S3 client used by CI/CD runs to upload artifacts directly to S3-compatible storage (LocalStack, AWS S3, Backblaze B2)

**Registry:** `ghcr.io/issuepit/issuepit-helper-base`

---

### `issuepit-helper-act`

Extends `helper-base` with [nektos/act](https://github.com/nektos/act), which lets you run GitHub Actions workflows locally inside the container — used by `IssuePit.CiCdClient` for local CI runs.

**Includes:** everything in `helper-base` + `act`

**Registry:** `ghcr.io/issuepit/issuepit-helper-act`

---

### `issuepit-helper-opencode`

Extends `helper-base` with the [opencode CLI](https://github.com/anomalyco/opencode), an AI-powered coding agent.

**Includes:** everything in `helper-base` + `opencode-ai` (npm global)

**Registry:** `ghcr.io/issuepit/issuepit-helper-opencode`

---

### `issuepit-helper-opencode-act`

Combines `helper-act` with the opencode CLI — the **default image for agent runs**. Provides full support for Docker-in-Docker (DinD) so agent tools can spawn containers, run CI workflows via `act`, and access all build tooling.

**Includes:** everything in `helper-act` (Docker Engine, act, actionlint) + `opencode-ai` (npm global)

**Registry:** `ghcr.io/issuepit/issuepit-helper-opencode-act`

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
| `PLAYWRIGHT_VERSION` | `helper-base` | `v1.51.0` | Playwright .NET image tag (e.g. `v1.54.0`) |
| `NODE_MAJOR` | `helper-base` | `24` | Node.js major version |
| `BASE_IMAGE` | `helper-act`, `helper-opencode`, `helper-opencode-act` | `ghcr.io/issuepit/issuepit-helper-base:latest` | Base image reference |
| `ACT_VERSION` | `helper-act` | `0.2.74` | nektos/act release version |
| `OPENCODE_VERSION` | `helper-opencode`, `helper-opencode-act` | `latest` | opencode-ai npm package version |

### Building locally

```bash
# Build the base image
docker build \
  --build-arg PLAYWRIGHT_VERSION=v1.51.0 \
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

# Build the opencode-act combined image (default for agent runs)
docker build \
  --build-arg BASE_IMAGE=issuepit-helper-act:local \
  -f docker/Dockerfile.helper-opencode-act \
  -t issuepit-helper-opencode-act:local \
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

---

## DinD Image Caching

When `issuepit-helper-act` runs CI/CD workflows it starts a full Docker daemon inside the container (Docker-in-Docker / DinD). By default, pulled images accumulate in an ephemeral overlay filesystem that is discarded when the container exits, so every run re-downloads all base images from Docker Hub.

The `DockerCiCdRuntime` supports three caching strategies that dramatically reduce pull times.

### Strategies

#### `Off` — no caching (ephemeral)

Each DinD container starts with an empty image store. Images are pulled fresh every run.

**Use when:** disk space is constrained, or strict reproducibility is required.

#### `LocalVolume` — persistent `/var/lib/docker` volume

A host directory is bind-mounted as `/var/lib/docker` inside the DinD container so pulled layers survive across runs.

**Pros:** simple, zero extra containers, effective layer cache.  
**Cons:** requires `Privileged=true` (already required for DinD); the volume grows over time.

**Security:** the volume directory must be dedicated to this purpose and not shared with other runtimes or processes.

**Disk management:** monitor the volume and prune periodically:
```bash
# Remove unused images from the DinD cache volume
docker run --rm --privileged \
  -v /var/lib/issuepit-dind-cache:/var/lib/docker \
  docker:dind docker system prune -f
```

#### `RegistryMirror` — pull-through registry mirror + volume *(default)*

Combines the persistent volume from `LocalVolume` with a `registry:2` sidecar container running as a pull-through cache for Docker Hub. The DinD `dockerd` is configured to route all image pulls through the local mirror; cache hits bypass Docker Hub entirely.

**Pros:** reduces upstream bandwidth and is horizontally scalable — the registry storage can be placed on a shared NFS/block volume accessible by multiple runner hosts.  
**Cons:** an additional `issuepit-registry-mirror` container is started on the host (once, managed automatically). Only public images are cached; private registry credentials are not forwarded.

**Aspire:** the `registry-mirror` resource is declared in the Aspire AppHost and started automatically with a persistent Docker volume (`issuepit-registry-cache`) on port 5100. The runtime reuses this container when it is already running. `cicd-client` waits for it to be healthy before accepting CI/CD triggers.

**Failure behavior:** if the registry is unavailable, CI/CD runs fail — there is no silent fallback. To restore the previous fallback-to-`LocalVolume` behavior, wrap the `EnsureRegistryMirrorAsync` call in `DockerCiCdRuntime` in a try/catch.

---

### Configuration

All settings are environment variables on the `cicd-client` service.

| Variable | Default | Description |
|----------|---------|-------------|
| `CiCd__Docker__DindCacheStrategy` | `RegistryMirror` | Cache strategy: `Off`, `LocalVolume`, or `RegistryMirror` |
| `CiCd__Docker__DindCacheVolumePath` | `/var/lib/issuepit-dind-cache` | Host path mounted as `/var/lib/docker` inside DinD containers |
| `CiCd__Docker__RegistryMirrorPort` | `5100` | Host port the `registry:2` mirror container listens on |
| `CiCd__Docker__RegistryMirrorVolumePath` | `/var/lib/issuepit-registry-cache` | Host path for registry mirror data |

The strategy can also be overridden per-run via the `DindCacheStrategy` field in the Kafka trigger payload (used by the retry endpoint).

### Example: docker-compose override

```yaml
services:
  cicd-client:
    environment:
      CiCd__Docker__DindCacheVolumePath: /data/dind-cache
      CiCd__Docker__RegistryMirrorPort: "5100"
      CiCd__Docker__RegistryMirrorVolumePath: /data/registry-cache
    volumes:
      - /data/dind-cache:/data/dind-cache
      - /data/registry-cache:/data/registry-cache
      - /var/run/docker.sock:/var/run/docker.sock
```

### Disk and cleanup

| Path | Owner | Cleanup command |
|------|-------|-----------------|
| `DindCacheVolumePath` | DinD image layers | `docker run --rm --privileged -v <path>:/var/lib/docker docker:dind docker system prune -af` |
| `RegistryMirrorVolumePath` | Registry blobs and manifests | `rm -rf <path>/docker/registry/v2/blobs/*` (stops serving blobs; safe to delete) |
