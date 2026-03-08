---
title: Home
layout: home
nav_order: 1
---

# IssuePit Documentation

**IssuePit** is a self-hosted project management platform that combines issue tracking, kanban boards, and an AI agent orchestration layer. Agents can be assigned to issues, run in containers, and report back results — all within the same platform.

---

## What you can do with IssuePit

- 🗂 **Track issues** across multiple projects and organizations
- 📋 **Kanban boards** with drag-and-drop cards (Backlog → Todo → In Progress → In Review → Done)
- 🤖 **AI agents** that automatically work on issues — agent modes define behavior, work agents execute them in Docker/Podman containers
- 🔗 **Link GitHub repositories** to your projects
- 🏷 **Labels, milestones, assignees** (users or agents)
- 🎯 **Common Agenda** — org-wide goal tracker for cross-cutting initiatives across all projects
- 🔄 **CI/CD integration** — run GitHub Actions workflows locally via [nektos/act](https://github.com/nektos/act) with real-time logs and artifact downloads
- ✅ **Todo Tracker** — personal task board and weekly calendar with iCal subscription
- 🔍 **Code review** — side-by-side diff viewer with inline comments
- 🔗 **Issue linking** — relate issues with typed links (blocks, implements, duplicates, and more)
- 📜 **Issue history** — full audit trail of every change made to an issue
- 🔀 **Merge requests** — lightweight merge request workflow on top of your linked Git repository
- 🎤 **Voice input** — create issues by dictating them using voice recognition (Vosk)

---

## Screenshots

![IssuePit Dashboard]({{ '/assets/screenshots/dashboard.png' | relative_url }})

*Dashboard — overview of projects, open issues, and agent count.*

![Projects list]({{ '/assets/screenshots/projects.png' | relative_url }})

*Projects page — each project card shows its slug and description.*

![Issue list]({{ '/assets/screenshots/issues.png' | relative_url }})

*Issue list — filterable by status, priority, and type.*

![Kanban board]({{ '/assets/screenshots/kanban.png' | relative_url }})

*Kanban board — issues organised across Backlog → To Do → In Progress → In Review → Done.*

![Agents page]({{ '/assets/screenshots/agents.png' | relative_url }})

*Agents — Plan, Code, and Evaluate agent modes ready to be activated.*

---

## Quick Navigation

| Page | Description |
|------|-------------|
| [Getting Started](getting-started) | Install and start IssuePit with Podman or Docker Compose |
| [Projects](projects) | Create projects and link Git repositories |
| [Common Agenda](agenda) | Org-wide goal tracker for cross-cutting initiatives |
| [CI/CD Integration](cicd) | Run GitHub Actions workflows locally with real-time logs |
| [Todo Tracker](todos) | Personal task board and weekly calendar with iCal subscription |
| [Agents](agents) | Configure agent modes (system prompt, MCP tools, auth keys) |
| [Configuration](configuration) | API keys, MCP servers, and other settings |
| [Releases](releases) | Release notes, changelog, and upgrade instructions |
| [Architecture](architecture) | Project layout, tech stack, and backend services |
| [Known Issues](known-issues) | Common setup problems and fixes |
| [FAQ](faq) | Frequently asked questions |
