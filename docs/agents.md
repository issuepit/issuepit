---
title: Agents
layout: default
nav_order: 4
---

# Agents

IssuePit supports **AI agents** that can be automatically assigned to issues and execute tasks inside containers. This page explains how to create and configure agent modes.

---

## Terminology

IssuePit distinguishes between two related concepts:

| Term | Definition |
|------|------------|
| **Agent mode** | A _configuration_ that defines a system prompt, MCP tools, authentication keys, and other settings. This is what you create and manage in the IssuePit UI. |
| **(Work) agent** | The actual CLI tool or process (e.g. `opencode`, `codex`, GitHub Copilot CLI) that performs the work. It is launched with an agent mode configuration. |

> **In short:** an agent mode is executed _by_ a work agent. Multiple agent modes can be run by multiple work agents in parallel.
>
> Work agents are spawned by `IssuePit.ExecutionClient` with the agent mode configuration on demand whenever there is work to be done.

---

## What is an Agent Mode?

An agent mode is a configuration that:
- Has a **system prompt** defining the role and behavior
- Optionally uses **MCP (Model Context Protocol) servers** for additional tools
- References authentication **API keys** for AI providers and external services
- Can be assigned to issues in the **Plan**, **Code**, or **Evaluate** queues

When an issue is assigned to an agent mode, `IssuePit.ExecutionClient` spawns a work agent (e.g. inside a Docker/Podman container) and passes it the agent mode configuration.

---

## Creating an Agent Mode

![Agents page]({{ '/assets/screenshots/agents.png' | relative_url }})

1. Go to **Configuration → Agents** (or **Agents** in the sidebar).
2. Click **New Agent**.
3. Fill in the agent mode details:

   | Field | Description |
   |-------|-------------|
   | **Name** | Display name (e.g. `Code Agent`) |
   | **System Prompt** | Instructions for the AI work agent (role, rules, tools) |
   | **Docker Image** | Container image for the work agent (e.g. `ghcr.io/sst/opencode:latest`) |
   | **Queue** | Which queue this agent mode handles: `Plan`, `Code`, or `Evaluate` |

   ![Create Agent dialog](https://github.com/user-attachments/assets/3cf747fb-9899-40cd-b852-ebd69b87ddb3)

4. Click **Save**.

---

## Agent Queues

IssuePit uses a **Plan → Code → Evaluate** pipeline:

| Queue | Purpose |
|-------|---------|
| **Plan** | Analyze the issue and create a detailed task breakdown |
| **Code** | Implement the changes (write code, create PRs) |
| **Evaluate** | Review the output and provide feedback or approve |

Each issue moves through these stages automatically when agents are assigned.

---

## Assigning an Agent Mode to an Issue

1. Open any issue.
2. In the **Assignees** section, click **Assign Agent**.
3. Select an agent mode from the list.
4. The agent mode is added to the queue and `IssuePit.ExecutionClient` spawns a work agent automatically.

---

## Container Runtimes

Work agents can run in different environments depending on your setup:

| Runtime | Description |
|---------|-------------|
| **Native** | Runs directly on the host machine |
| **Docker / Podman** | Runs in a local container |
| **SSH** | Connects to a remote machine via SSH |
| **Hetzner + Terraform** | Provisions a fresh Hetzner cloud VM per run |
| **OpenSandbox** | Uses an OpenSandbox-compatible environment |

To configure a runtime, go to **Agents → Runtimes**.

---

## MCP Servers

Agent modes can use **MCP (Model Context Protocol) servers** to access external tools such as GitHub, file systems, or custom APIs.

1. First, add an MCP server under **Agents → MCP Servers**.
2. Then link it to an agent in the agent's settings under **MCP Servers**.

---

## Tips for Writing System Prompts

- Be specific about what the work agent should and should not do.
- Include the programming language, framework, and coding conventions.
- Tell the work agent what to output (e.g. a Git commit, a PR description, a summary).

**Example prompt for a code agent:**

```
You are a senior TypeScript developer working on a Nuxt 3 / Vue 3 frontend.
When assigned an issue, implement the described feature or fix in the codebase.
Follow existing code conventions. Create a conventional commit message.
Do not modify unrelated files.
```

---

## How the Execution Client Works

When an agent mode is assigned to an issue, `IssuePit.ExecutionClient` handles the lifecycle:

1. Consumes the `issue-assigned` Kafka topic.
2. Spawns a work agent (e.g. `opencode`, `codex`, GitHub Copilot CLI) inside a Docker-in-Docker container with the agent mode configuration.
3. Copies agent authentication logins into the workspace container.
4. Streams results back to the platform.
5. Supports provisioning Hetzner cloud VMs for resource-intensive (vibe-coding) agents.

---

## How the CI/CD Client Works

`IssuePit.CiCdClient` integrates with local CI pipelines:

1. Consumes the `cicd-trigger` Kafka topic.
2. Runs GitHub Actions workflows locally via [nektos/act](https://github.com/nektos/act).

---

## Coding Agent Guidelines

When working as a coding agent on this repository, follow these conventions:

### Frontend Changes

- **Always run the frontend linter before closing a session:**
  ```sh
  cd frontend && node_modules/.bin/eslint .
  ```
  Fix all lint **errors** (unused variables, type-only imports, etc.). Pre-existing warnings from unrelated code do not need to be resolved.

### PR Screenshots

Screenshots in PRs and documentation must show the actual UI **after authentication** — the login page is not useful. The `scripts/take-screenshots.js` script handles this by:

1. Logging in (or registering) a user before taking any screenshots.
2. Accepting a pre-seeded user via `SCREENSHOT_USERNAME` / `SCREENSHOT_PASSWORD` environment variables (the Aspire Migrator seeds `alice`/`alice` for the Acme Corp org).

To take screenshots manually during development:

```sh
# Start the full stack first (Aspire), then:
FRONTEND_URL=http://localhost:3000 \
API_URL=http://localhost:5000 \
SCREENSHOT_USERNAME=alice \
SCREENSHOT_PASSWORD=alice \
node scripts/take-screenshots.js /tmp/screenshots
```

Always verify that uploaded screenshots show the intended UI (not a blank page, wrong page, or the login screen).

---

## Next Steps

- [API Keys and MCP Servers →](configuration)
