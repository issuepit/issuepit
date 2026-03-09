---
title: Releases
layout: default
nav_order: 8
---

# Releases

IssuePit follows [Semantic Versioning](https://semver.org/). Releases are managed via [Release Please](https://github.com/googleapis/release-please) and published automatically when changes are merged to `main`.

---

## Latest Release

Visit the [GitHub Releases page](https://github.com/issuepit/issuepit/releases/latest) for the latest version, release notes, and downloadable assets.

---

## Release Assets

Each release includes:

| Asset | Description |
|-------|-------------|
| `docker-compose.yml` | Compose file to run the full IssuePit stack |
| Source code (zip / tar.gz) | Full repository snapshot |

---

## Changelog

The full history of changes is available in the [CHANGELOG](https://github.com/issuepit/issuepit/blob/main/CHANGELOG.md).

---

## Upgrading

1. Download the new `docker-compose.yml` from the [releases page](https://github.com/issuepit/issuepit/releases).
2. Stop the running stack:
   ```bash
   podman compose down
   # or
   docker compose down
   ```
3. Replace your existing `docker-compose.yml` with the new one.
4. Pull the new images and restart:
   ```bash
   podman compose pull && podman compose up -d
   # or
   docker compose pull && docker compose up -d
   ```

Database migrations run automatically on startup.

---

## Helper Containers

The [helper container images](developer/helper-containers) are versioned independently and published under the `ghcr.io/issuepit/` namespace.
