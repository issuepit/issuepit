# Workflow Guidelines for Agents

This document covers conventions and constraints for the GitHub Actions workflows in this directory. Read this before modifying or adding workflow files.

---

## General Rules

- Keep workflow changes minimal and focused — one logical change per commit.
- Workflow files are YAML; test any structural changes locally with `act` if possible.
- Do not increase CI job timeouts as a workaround for slow builds — investigate and fix the root cause.
- Workflow trigger paths (`paths:`) must be kept in sync with the files they depend on.

---

## The `ISSUEPIT_RUN` Guard

```yaml
if: vars.ISSUEPIT_RUN != 'true'
```

Many jobs and workflows include this condition. **Do not remove it.**

### Why it exists

When IssuePit runs a workflow internally (e.g. as a CI/CD run triggered from its own UI), the GitHub Actions `GITHUB_TOKEN` issued to the runner only has `contents: read` equivalent — it does **not** have `packages: write` or other elevated permissions. Basicly it has not access to github.com itself and uses `act` to mimick some functionallity. This means:

- Pushing images to GHCR (`ghcr.io`) will fail. Same with reading metadata
- Creating GitHub releases or writing to the repository may also fail.

Setting `vars.ISSUEPIT_RUN = true` in the repository variables signals that the current run is being executed inside the IssuePit platform. Jobs that require these permissions must be skipped.

### When to apply this guard

Apply `if: vars.ISSUEPIT_RUN != 'true'` to any job that:
- Pushes container images to a registry (GHCR, Docker Hub, etc.)
- Creates or modifies GitHub releases or tags
- Requires `packages: write`, `contents: write`, or `deployments: write` permissions

Jobs that only build, test, or lint (without publishing) do **not** need this guard.

---

## Helper Container Images (`helper-containers.yml`)

### Image build order

The `build` job builds images sequentially in a single job (using the `docker` Buildx driver). This ensures locally-built images are available for subsequent `FROM` references without a registry push.

Build order and dependencies:

```
helper-base          ← mcr.microsoft.com/playwright/dotnet (external)
├── helper-act       ← helper-base:local + Docker Engine + act + actionlint
│   └── helper-opencode-act  ← helper-act:local + opencode
├── helper-opencode  ← helper-base:local + opencode
└── issuepit-act-runner  ← helper-base:local + ffmpeg + jq
```

**Do not reorder the build steps** — later images depend on earlier ones being present in the local daemon.

### Image naming

| Local tag | Published as | Purpose |
|---|---|---|
| `helper-base:local` | `ghcr.io/issuepit/issuepit-helper-base` | Base for all helper containers: .NET SDK, Playwright + Chrome, Node.js, s5cmd |
| `helper-act:local` | `ghcr.io/issuepit/issuepit-helper-act` | Outer container that runs `act` (the CI/CD job runner) |
| `helper-opencode:local` | `ghcr.io/issuepit/issuepit-helper-opencode` | Outer container that runs `opencode` (the AI agent) |
| `helper-opencode-act:local` | `ghcr.io/issuepit/issuepit-helper-opencode-act` | Outer container for opencode + act combined |
| `act-runner:local` | `ghcr.io/issuepit/issuepit-act-runner` | **Inner** runner image for `act`'s `-P ubuntu-latest=<image>` mapping |

### Outer vs inner images

**Outer** containers (helper-act, helper-opencode, helper-opencode-act) are the containers IssuePit launches to run agent or CI/CD jobs. They include the agent entrypoint script (`entrypoint.sh`) that handles git cloning, tool setup, and DNS proxying.

**Inner** runner images (issuepit-act-runner) are the images used _by_ `act` for workflow job containers (the `-P ubuntu-latest=<image>` platform mapping). They must **not** inherit the agent entrypoint because:
- `act` starts them differently (direct command execution, not agent bootstrapping)
- DNS proxy and git-push-blocking wrappers from `entrypoint.sh` would interfere with normal workflow steps

`Dockerfile.helper-act-runner` explicitly resets `ENTRYPOINT []` and `CMD ["/bin/bash"]` to neutralise the `entrypoint.sh` inherited from `helper-base`.

### Adding a new helper image

1. Create `docker/helper-containers/Dockerfile.helper-<name>`.
2. Add an `ARG BASE_IMAGE` with the appropriate local tag as default.
3. Add a metadata extraction step (`meta-<name>`) to `helper-containers.yml`.
4. Add a build step **after** its dependency is built.
5. Add the new image to the `push_image` calls in the push step.
6. Update this table above.

---

## Frontend & Backend CI (`frontend.yml`, `backend.yml`)

- `frontend.yml` runs lint, type-check, and build on every PR affecting `frontend/**`.
- `backend.yml` runs .NET build, unit tests, and E2E tests on every PR affecting `src/**`.
- Both run on the IssuePit helper image (`ghcr.io/issuepit/issuepit-helper-act`) so that Chrome, .NET, and Node.js are available without extra installation steps.
- The E2E tests require the Aspire stack (Postgres, Kafka, Redis) to be running — this is handled by the `AspireFixture` in `src/IssuePit.Tests.E2E/`.
