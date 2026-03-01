# IssuePit

**Agent Orchestration Platform with Issue Tracking and Kanban Board**

IssuePit is a self-hosted project management platform that combines issue tracking (Jira-like), kanban boards, and an AI agent orchestration layer. Agents can be assigned to issues, run in Docker containers, and report back results — all within the same platform.

---

## Architecture

```
src/
├── IssuePit.AppHost/          # .NET Aspire 9 orchestration host
├── IssuePit.ServiceDefaults/  # Shared Aspire service defaults (OpenTelemetry, resilience)
├── IssuePit.Core/             # Domain models, EF Core entities, DbContext
├── IssuePit.Api/              # ASP.NET Core 10 Web API (REST endpoints)
├── IssuePit.ExecutionClient/  # Worker: consumes Kafka, runs agents in Docker
└── IssuePit.CiCdClient/       # Worker: consumes Kafka, triggers CI/CD via act

frontend/                      # Vue 3 + Nuxt 3 + Pinia frontend
```

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
- Define agents: system prompt, default Docker image, allowed tools
- Manage MCP servers and link them to agents
- Multi-agent, parallel execution runtime
- Container runtimes: native, SSH, Docker, Hetzner+Terraform+SSH, OpenSandbox

### Execution Client
- Consumes `issue-assigned` Kafka topic
- Runs agents (opencode, codex, GitHub Copilot CLI) in Docker (DinD)
- Copies agent logins to workspace containers
- Supports spawning Hetzner machines for vibe-coding agents

### CI/CD Client
- Consumes `cicd-trigger` Kafka topic
- Runs GitHub Actions locally via [nektos/act](https://github.com/nektos/act)

---

## Technology Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 10, C# |
| Orchestration | .NET Aspire 13 |
| Database | PostgreSQL 17 + EF Core 9 (Npgsql) |
| Messaging | Apache Kafka (Confluent .NET client) |
| Frontend | Vue 3, Nuxt 3, Pinia, TailwindCSS |
| Containers | Docker, Docker-in-Docker |
| Observability | OpenTelemetry (traces, metrics, logs) |

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Docker](https://www.docker.com/) or better [Podman](https://podman.io/)

### Run with Aspire

```bash
aspire run
```
or
```bash
cd src
dotnet run --project IssuePit.AppHost
```

The Aspire dashboard will start at `https://localhost:15888`. The API, PostgreSQL, and Kafka are provisioned automatically.

### Frontend (dev)

```bash
cd frontend
npm install
npm run dev
```

The frontend dev server runs at `http://localhost:3000` and proxies API calls to the Aspire-managed backend.

---

## Development

See [agents.md](agents.md) for coding conventions, commit guidelines, and the Definition of Done used by both human developers and AI agents.

---

## Project Structure — Backend

### `IssuePit.Core`
Domain models and EF Core `IssuePitDbContext`. All entity configuration is done via data annotations on the entity classes.

Key entities: `Tenant`, `Organization`, `Project`, `Issue`, `IssueTask`, `Label`, `Milestone`, `Agent`, `McpServer`, `KanbanBoard`, `KanbanColumn`, `User`

### `IssuePit.Api`
Minimal API endpoints grouped by resource. Multi-tenant middleware resolves the current tenant from the `X-Tenant-Id` request header or the request hostname. Kafka producer publishes events when issues are created or assigned.

### `IssuePit.ExecutionClient`
Background worker that subscribes to the `issue-assigned` Kafka topic. Uses `Docker.DotNet` to spin up agent containers and stream results back.

### `IssuePit.CiCdClient`
Background worker that subscribes to the `cicd-trigger` Kafka topic and drives local CI runs via `act`.


# Known issue:

## aspire ssl outdated
```bash
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```
## aspire cli not found
https://aspire.dev/get-started/install-cli/
```bash
export PATH="/c/Users/user/.aspire/bin:$PATH"
```

## frontend is not starting inside of aspire:
```bash
cd frontend
npm ci
```

---

## License

will later be opensourced, currently is just source available and copyrighted by issuepit