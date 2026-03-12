---
title: FAQ
layout: default
nav_order: 12
parent: Developer
---

# FAQ

---

## Can I use IssuePit without AI agents?

Yes. IssuePit works as a standalone issue tracker and kanban board. AI agent features are optional and only activate when you configure agent modes and assign them to issues.

---

## What AI providers are supported?

Any provider accessible via a compatible CLI agent (e.g. `opencode`, `codex`, GitHub Copilot CLI). Authentication keys for OpenAI, Anthropic, Azure OpenAI, and Google (Gemini) can be stored in **Configuration → API Keys**.

---

## Does IssuePit require Docker?

Yes. Docker (or Podman) is required in all environments. For development, [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) orchestrates the services but still relies on Docker to run the required infrastructure containers (PostgreSQL, Kafka, Redis). For production, Docker Compose is the recommended deployment approach.

---

## Where are agents executed?

By default, work agents run inside Docker/Podman containers on the same host. You can also configure SSH remotes, Hetzner cloud VMs, or OpenSandbox environments under **Configuration → Agent Runtimes**.

---

## How do I upgrade IssuePit?

Pull the latest `docker-compose.yml` from the [releases page](https://github.com/issuepit/issuepit/releases), then run:

```bash
docker compose pull
docker compose up -d
```

See the [Releases](releases) page for version-specific upgrade notes.
