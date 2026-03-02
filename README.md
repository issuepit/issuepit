# IssuePit

**Agent Orchestration Platform with Issue Tracking and Kanban Board**

IssuePit is a self-hosted project management platform that combines issue tracking (Jira-like), kanban boards, and an AI agent orchestration layer. Agents can be assigned to issues, run in Docker containers, and report back results — all within the same platform.

📖 **[User Documentation](https://issuepit.github.io/issuepit/)** — guides, screenshots, and configuration reference (GitHub Pages, source in `docs/`)

---

## Features

### Issue Tracking
- Multi-tenant (by hostname or `X-Tenant-Id` header; supports separate databases per tenant)
- Organizations, projects, issues, sub-issues, tasks
- Labels, milestones, assignees (users or agents)
- Link / import GitHub issues (copy issue number)
- Link Git(Hub) repositories to projects
- Issue and task branches (per-issue Git branch)
- Different agent queues: **Plan → Code → Evaluate**

### Kanban Board
- Visual columns: Backlog → Todo → In Progress → In Review → Done
- Drag-and-drop cards; per-project board configuration

### Agent Management
- Define agent modes: system prompt, default Docker image, allowed tools
- Manage MCP servers and link them to agent modes
- Multi-agent, parallel execution runtime
- Container runtimes: native, SSH, Docker, Hetzner+Terraform+SSH, OpenSandbox

### Execution Client
- Runs AI agents (opencode, codex, GitHub Copilot CLI) in containers on demand
- Supports multiple container runtimes including Hetzner cloud VMs

### CI/CD Client
- Runs GitHub Actions locally via [nektos/act](https://github.com/nektos/act)

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Docker](https://www.docker.com/) or [Podman](https://podman.io/)

### Run with Aspire (Development)

> **Note:** Before starting Aspire, run `npm ci` in the `frontend` folder so the frontend can start correctly inside Aspire.

```bash
cd frontend && npm ci && cd ..
```

Then start Aspire:

```bash
aspire run
```
or
```bash
cd src
dotnet run --project IssuePit.AppHost
```

The Aspire dashboard will start at `https://localhost:15888`. The API, PostgreSQL, Kafka, and frontend are provisioned automatically.

### Run with Podman Compose

```bash
podman compose up -d
```

Or with Docker:

```bash
docker compose up -d
```

Open your browser at `http://localhost:3000` once all services are healthy.

---

## Documentation

| Topic | Link |
|-------|------|
| User documentation | [issuepit.github.io/issuepit](https://issuepit.github.io/issuepit/) |
| Architecture & tech stack | [docs/architecture.md](docs/architecture.md) |
| Development & coding conventions | [agents.md](agents.md) |
| Known issues | [docs/known-issues.md](docs/known-issues.md) |
| FAQ | [docs/faq.md](docs/faq.md) |

---

## License

will later be opensourced, currently is just source available and copyrighted by issuepit