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

---

## Screenshots

![IssuePit Dashboard](https://github.com/user-attachments/assets/1a80823d-41f0-4bf5-bf7a-3ca8f01dfab2)

*Dashboard — overview of projects, open issues, and agent count.*

![Projects list](https://github.com/user-attachments/assets/3fbde9ad-827a-4be3-a87d-880626634340)

*Projects page — each project card shows its slug and description.*

![Issue list](https://github.com/user-attachments/assets/63af9ac8-f5e7-49c7-9254-894e6172fc05)

*Issue list — filterable by status, priority, and type.*

![Kanban board](https://github.com/user-attachments/assets/19c72963-a4c9-463f-a692-f6e2d6aeea1d)

*Kanban board — issues organised across Backlog → To Do → In Progress → In Review → Done.*

![Agents page](https://github.com/user-attachments/assets/221ddf9b-6e1b-49e3-8302-5d47dbecbf0a)

*Agents — Plan, Code, and Evaluate agent modes ready to be activated.*

---

## Quick Navigation

| Page | Description |
|------|-------------|
| [Getting Started](getting-started) | Install and start IssuePit with Podman or Docker Compose |
| [Projects](projects) | Create projects and link Git repositories |
| [Agents](agents) | Configure agent modes (system prompt, MCP tools, auth keys) |
| [Configuration](configuration) | API keys, MCP servers, and other settings |
