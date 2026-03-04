---
title: DinD Image Cache
layout: default
parent: Developer
nav_order: 3
---

# DinD Image Caching

IssuePit's CI/CD runner uses Docker-in-Docker (DinD) to run GitHub Actions workflows inside
isolated containers via [nektos/act](https://github.com/nektos/act). By default each run starts
with a cold Docker image store, which means every job must re-pull all runner and action images
from the registry.

The **DinD image cache** feature lets you persist those layers across runs so subsequent runs
start much faster.

---

## Strategies

Three strategies are available, selected via the `CiCd__DindCache__Strategy` environment variable
(or `appsettings.json`):

| Strategy | Description | Concurrency safe? |
|---|---|---|
| `None` | No cache — fresh image store per run. | ✅ |
| `Volume` (**default**) | Mounts a host directory as `/var/lib/docker` inside the DinD container so pulled layers survive between runs. | ⚠️ Serial only |
| `RegistryMirror` | Runs a `registry:2` pull-through cache container; DinD is configured to use it as a mirror. Also mounts a `/var/lib/docker` volume for the DinD container's own layer store. | ✅ |

> **Default:** `Volume` — the simplest option that provides effective caching for single-runner setups.

---

## Configuration reference

All settings live under the `CiCd__DindCache` prefix (double-underscore notation for environment
variables, colon-separated in JSON):

| Key | Default | Description |
|---|---|---|
| `Strategy` | `Volume` | Caching strategy: `None`, `Volume`, or `RegistryMirror`. |
| `VolumePath` | `/var/cache/issuepit/docker` | Host path mounted as `/var/lib/docker` inside the DinD container. Applies to `Volume` and `RegistryMirror` strategies. |
| `RegistryMirrorImage` | `registry:2` | Docker image used for the pull-through registry mirror. |
| `RegistryMirrorContainerName` | `issuepit-registry-mirror` | Name of the mirror container managed by IssuePit. |
| `RegistryMirrorPort` | `5555` | Host port the registry listens on. |
| `RegistryMirrorVolumePath` | `/var/cache/issuepit/registry` | Host path mounted as `/var/lib/registry` inside the mirror container. |
| `RegistryMirrorHost` | `172.17.0.1` | IP or hostname reachable from inside the DinD container that points to the mirror. Use the Docker bridge gateway IP (usually `172.17.0.1` on Linux). |

### Per-organization and per-project override

You can override the strategy per organization or per project through the API (field
`dindCacheStrategy` on the org/project object, values `null`/`0`=None, `1`=Volume, `2`=RegistryMirror).
`null` means "inherit the global default".

Precedence: **trigger payload → project → organization → global config**.

---

## Variant 1 — Local persistent volume (`Volume`)

### How it works

When a CI/CD run starts the Docker runtime mounts the configured `VolumePath` as `/var/lib/docker`
inside the DinD container (with `Privileged=true`, which is already required for DinD). The
DinD daemon writes all pulled layers into this directory. On subsequent runs the same layers are
found on disk and do not need to be re-pulled.

### Setup

1. Create the cache directory on the host (or let IssuePit create it automatically):
   ```sh
   sudo mkdir -p /var/cache/issuepit/docker
   ```
2. Set the strategy in `appsettings.json` (or via environment variable):
   ```json
   "CiCd": {
     "DindCache": {
       "Strategy": "Volume",
       "VolumePath": "/var/cache/issuepit/docker"
     }
   }
   ```

### ⚠️ Concurrency warning

Multiple Docker daemons **cannot safely share** the same data-root directory. If you run more than
one CI/CD job concurrently (per-org `MaxConcurrentRunners > 1`) with `Volume` strategy the daemons
will conflict and data corruption can occur.

**Recommendation:** Use `Volume` only when `MaxConcurrentRunners = 1` (serial runs). Switch to
`RegistryMirror` for concurrent setups.

### Disk management

Pulled layers accumulate over time. Periodically prune the cache:

```sh
# Start a temporary container that can reach the DinD cache directory and prune
docker run --rm --privileged -v /var/cache/issuepit/docker:/var/lib/docker \
  docker:dind docker system prune -f
```

---

## Variant 2 — Pull-through registry mirror (`RegistryMirror`)

### How it works

When `RegistryMirror` strategy is selected, IssuePit automatically:

1. Pulls `registry:2` if not already present.
2. Creates and starts an `issuepit-registry-mirror` container that acts as a transparent HTTP
   proxy for Docker Hub (`registry-1.docker.io`). Registry data is stored in `RegistryMirrorVolumePath`.
3. Configures each DinD daemon to use `http://<RegistryMirrorHost>:<RegistryMirrorPort>` as a
   registry mirror via `dockerd --registry-mirror=...`.
4. Also mounts the `VolumePath` as `/var/lib/docker` for the DinD container's own layer store.

The mirror container is **shared across all concurrent runs**, making this strategy safe for
parallel CI/CD jobs and suitable for multi-runner deployments.

### Setup

```json
"CiCd": {
  "DindCache": {
    "Strategy": "RegistryMirror",
    "VolumePath": "/var/cache/issuepit/docker",
    "RegistryMirrorVolumePath": "/var/cache/issuepit/registry",
    "RegistryMirrorPort": 5555,
    "RegistryMirrorHost": "172.17.0.1"
  }
}
```

> **Linux tip:** The Docker bridge gateway is `172.17.0.1` by default. Verify with
> `docker network inspect bridge | grep Gateway`.

### Fallback behavior

If the mirror container cannot be created or started for any reason (e.g. insufficient permissions,
port already in use), IssuePit logs a warning and the run continues **without** the mirror. This
means image pulls go directly to Docker Hub — no run is ever aborted solely because of mirror
setup failure.

### Disk management

Clear cached layers from the registry mirror volume:

```sh
docker rm -f issuepit-registry-mirror
sudo rm -rf /var/cache/issuepit/registry
# IssuePit will recreate the container on the next run.
```

---

## Security considerations

| Concern | Notes |
|---|---|
| Privileged containers | DinD always requires `Privileged=true`. This is not changed by the cache strategy. |
| `/var/lib/docker` volume | Exposes the host kernel's filesystem to the container. Use only on trusted hosts. |
| Registry mirror | The mirror only caches public Docker Hub images. Private registry pulls bypass it. |
| Disk growth | Both strategies accumulate data over time. Set up a pruning cron job in production. |

---

## Disabling the cache

Set `Strategy` to `None` to restore the original no-cache behavior:

```json
"CiCd": { "DindCache": { "Strategy": "None" } }
```
