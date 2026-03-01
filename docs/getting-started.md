---
title: Getting Started
layout: default
nav_order: 2
---

# Getting Started

This guide walks you through installing and running IssuePit on your own machine using **Podman** or **Docker Compose**.

---

## Prerequisites

- [Podman](https://podman.io/) 4+ (or Docker with Docker Compose)
- A modern web browser

---

## Running with Podman

### 1. Download the Compose file

Save the following as `docker-compose.yml`, or download it from the [releases page](https://github.com/issuepit/issuepit/releases):

```bash
curl -O https://raw.githubusercontent.com/issuepit/issuepit/main/docker-compose.yml
```

### 2. Start the stack

```bash
podman compose up -d
```

This will start the following services:
- **PostgreSQL 17** — database
- **Apache Kafka** — event streaming
- **Redis (Valkey)** — caching
- **IssuePit API** — backend REST API (port `5000`)
- **IssuePit Frontend** — web UI (port `3000`)
- **Execution Client** — runs agent containers
- **CI/CD Client** — triggers CI/CD pipelines

### 3. Open the application

Once all services are healthy, open your browser at:

```
http://localhost:3000
```

---

## Running with Docker Compose

If you use Docker instead of Podman, the same `docker-compose.yml` works with Docker Compose v2:

```bash
docker compose up -d
```

---

## Stopping the stack

```bash
podman compose down
# or
docker compose down
```

To also remove stored data (database volumes):

```bash
podman compose down -v
# or
docker compose down -v
```

---

## Configuration via Environment Variables

You can override any service configuration by editing `docker-compose.yml` or by creating a `.env` file in the same directory.

| Variable | Service | Description |
|----------|---------|-------------|
| `POSTGRES_PASSWORD` | postgres | Database password (default: `issuepit`) |
| `ConnectionStrings__issuepit-db` | api | Full PostgreSQL connection string |
| `Kafka__BootstrapServers` | api, clients | Kafka broker address |

---

## Next Steps

- [Create your first project →](projects)
- [Configure AI agents →](agents)
