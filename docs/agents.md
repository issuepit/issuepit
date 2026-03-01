---
title: Agents
layout: default
nav_order: 4
---

# Agents

IssuePit supports **AI work agents** that can be automatically assigned to issues and execute tasks inside containers. This page explains how to create and configure agents.

---

## What is an Agent?

An agent is a configured AI worker that:
- Has a **system prompt** defining its role and behavior
- Runs inside a **Docker/Podman container**
- Optionally uses **MCP (Model Context Protocol) servers** for additional tools
- Can be assigned to issues in the **Plan**, **Code**, or **Evaluate** queues

---

## Creating an Agent

1. Go to **Configuration → Agents** (or **Agents** in the sidebar).
2. Click **New Agent**.
3. Fill in the agent details:

   | Field | Description |
   |-------|-------------|
   | **Name** | Display name (e.g. `Code Agent`) |
   | **System Prompt** | Instructions for the AI (role, rules, tools) |
   | **Docker Image** | Container image to run (e.g. `ghcr.io/sst/opencode:latest`) |
   | **Queue** | Which queue the agent handles: `Plan`, `Code`, or `Evaluate` |

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

## Assigning an Agent to an Issue

1. Open any issue.
2. In the **Assignees** section, click **Assign Agent**.
3. Select an agent from the list.
4. The agent is added to the queue and the Execution Client picks it up automatically.

---

## Container Runtimes

Agents can run in different environments depending on your setup:

| Runtime | Description |
|---------|-------------|
| **Native** | Runs directly on the host machine |
| **Docker / Podman** | Runs in a local container |
| **SSH** | Connects to a remote machine via SSH |
| **Hetzner + Terraform** | Provisions a fresh Hetzner cloud VM per run |
| **OpenSandbox** | Uses an OpenSandbox-compatible environment |

To configure a runtime, go to **Configuration → Agent Runtimes**.

---

## MCP Servers

Agents can use **MCP (Model Context Protocol) servers** to access external tools such as GitHub, file systems, or custom APIs.

1. First, add an MCP server under **Configuration → MCP Servers**.
2. Then link it to an agent in the agent's settings under **MCP Servers**.

---

## Tips for Writing System Prompts

- Be specific about what the agent should and should not do.
- Include the programming language, framework, and coding conventions.
- Tell the agent what to output (e.g. a Git commit, a PR description, a summary).

**Example prompt for a code agent:**

```
You are a senior TypeScript developer working on a Nuxt 3 / Vue 3 frontend.
When assigned an issue, implement the described feature or fix in the codebase.
Follow existing code conventions. Create a conventional commit message.
Do not modify unrelated files.
```

---

## Next Steps

- [API Keys and MCP Servers →](configuration)
