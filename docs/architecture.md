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
├── IssuePit.AppHost/          # .NET Aspire 9 orchestration host
├── IssuePit.ServiceDefaults/  # Shared Aspire service defaults (OpenTelemetry, resilience)
├── IssuePit.Core/             # Domain models, EF Core entities, DbContext
├── IssuePit.Api/              # ASP.NET Core 10 Web API (REST endpoints)
├── IssuePit.ExecutionClient/  # Worker: consumes Kafka, runs agents in Docker
└── IssuePit.CiCdClient/       # Worker: consumes Kafka, triggers CI/CD via act

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

### `IssuePit.ExecutionClient`

Background worker that subscribes to the `issue-assigned` Kafka topic. Uses `Docker.DotNet` to spin up agent containers and stream results back.

### `IssuePit.CiCdClient`

Background worker that subscribes to the `cicd-trigger` Kafka topic and drives local CI runs via `act`.
