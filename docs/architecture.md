---
title: Architecture
layout: default
nav_order: 10
parent: Developer
---

# Architecture

This page describes the high-level structure of IssuePit, its technology stack, and the responsibilities of each backend service.

---

## Project Layout

```
src/
├── IssuePit.AppHost/          # .NET Aspire orchestration host
├── IssuePit.ServiceDefaults/  # Shared Aspire service defaults (OpenTelemetry, resilience)
├── IssuePit.Core/             # Domain models, EF Core entities, DbContext
├── IssuePit.Api/              # ASP.NET Core Web API (REST endpoints)
├── IssuePit.McpServer/        # MCP server exposing IssuePit tools to AI agents
├── IssuePit.Migrator/         # EF Core database migrations runner
├── IssuePit.KafkaInitializer/ # Kafka topic setup on startup
├── IssuePit.ExecutionClient/  # Worker: consumes Kafka, runs agents in Docker
├── IssuePit.CiCdClient/       # Worker: consumes Kafka, triggers CI/CD via act
├── IssuePit.Notes.Core/       # Notes module: domain models, NotesDbContext (separate DB)
├── IssuePit.Notes.Api/        # Notes module: REST API (standalone service)
└── IssuePit.Notes.Migrator/   # Notes module: database migrations runner

frontend/                      # Vue 3 + Nuxt 3 + Pinia frontend
```

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

## Backend Services

### `IssuePit.Core`

Domain models and EF Core `IssuePitDbContext`. All entity configuration is done via data annotations on the entity classes.

Key entities: `Tenant`, `Organization`, `Project`, `Issue`, `IssueTask`, `Label`, `Milestone`, `Agent`, `McpServer`, `KanbanBoard`, `KanbanColumn`, `User`

### `IssuePit.Api`

Minimal API endpoints grouped by resource. Multi-tenant middleware resolves the current tenant from the `X-Tenant-Id` request header or the request hostname. Kafka producer publishes events when issues are created or assigned.

### `IssuePit.McpServer`

MCP (Model Context Protocol) server that exposes IssuePit tools (issue management, project queries, etc.) to AI agents. Referenced by the API for issue enhancement workflows.

### `IssuePit.Migrator`

Runs EF Core database migrations at startup. Aspire waits for it to complete before starting the API and other services.

### `IssuePit.KafkaInitializer`

Creates required Kafka topics at startup. Aspire waits for it to complete before starting consumers.

### `IssuePit.ExecutionClient`

Background worker that subscribes to the `issue-assigned` Kafka topic. Uses `Docker.DotNet` to spin up agent containers and stream results back.

### `IssuePit.CiCdClient`

Background worker that subscribes to the `cicd-trigger` Kafka topic and drives local CI runs via `act`.

### Notes Module (`IssuePit.Notes.*`)

Modular note-taking service, implemented as a separate backend with its own database. Designed for standalone deployment.

- **`IssuePit.Notes.Core`** — Domain models (`Note`, `NoteWorkspace`, `NoteLink`) and `NotesDbContext`. Uses a dedicated PostgreSQL database (`notes-db`) independent of the main IssuePit database.
- **`IssuePit.Notes.Api`** — REST API service providing CRUD endpoints for workspaces and notes, wiki-link extraction (`[[...]]` syntax), full-text search, graph data for visualization, and optimistic concurrency via version numbers. Tenant isolation uses the `X-Tenant-Id` header.
- **`IssuePit.Notes.Migrator`** — Runs EF Core migrations for the Notes database at startup.

The Notes module supports multiple storage engine types (Postgres, SQLite, Git, S3, Elasticsearch) via the `NoteStorageEngine` enum — only Postgres is implemented initially; others are prepared as extension points.

Notes can link to other notes via `[[Note Title]]` wiki-style syntax, and to IssuePit entities via `[[issue:ID]]`, `[[project:ID]]`, or `[[todo:ID]]` prefixed links.
