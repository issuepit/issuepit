---
title: Common Agenda
---

# Common Agenda

The **Common Agenda** is an organization-wide goal tracker that spans all projects. It lets you define cross-cutting initiatives — tasks that need to be applied consistently across every repository or project in your organization — and track their progress in one place.

---

## What is the Common Agenda?

A **Common Agenda project** is a regular IssuePit project marked as the org-wide agenda. It holds issues that represent goals applying to the entire organization rather than a single project.

Typical use cases:

| Goal | Example issue |
|------|--------------|
| **AI skills rollout** | Add `plan`, `code`, and `evaluate` agent modes to all repos |
| **Security tooling** | Add SAST scanner (Semgrep / CodeQL) to every CI/CD pipeline |
| **Library migration** | Migrate from `log4net` (EOL) to OpenTelemetry across all services |
| **CI/CD standardization** | Enforce branch protection rules and required status checks on all repos |
| **Docker hygiene** | Pin base images to distroless / minimal variants org-wide |
| **Dependency updates** | Upgrade all projects to the latest LTS version of Node / .NET |

---

## Setting Up a Common Agenda

1. **Create (or choose) a project** that will serve as your org-wide agenda.
2. Open the project's **Settings → General**.
3. Toggle **Common Agenda** to **on**.
4. Click **Save Changes**.

The project now appears in your organization's **Agenda** tab.

---

## Using the Agenda Tab

Navigate to **Organizations → [your org] → 🎯 Agenda** to see:

- All agenda projects in your org
- Every issue in each agenda project, with status and priority
- One-click navigation to any issue

---

## Linking Issues Across Projects

Each agenda issue can be linked to the specific implementation issues in individual projects using **Linked Issues**.

On any issue detail page:

1. Scroll to **Linked Issues**.
2. Click **Add link**.
3. Choose the link type (e.g. *implements*, *requires*, *linked to*).
4. Select any issue **from any project in your org** — not just the current project.
5. Click **Add**.

Cross-project links are clearly labelled with a **↗ cross-project** badge so you can tell at a glance when a link goes to another project.

---

## AI-Powered Pattern Analysis (planned)

> 📌 This feature is planned and will be implemented after [issue #257](https://github.com/issuepit/issuepit/issues/257).

Once pattern-analysis support is available, an AI agent can:

1. **Scan all repositories** in an org for design and architecture patterns.
2. **Write findings** into the agenda project's wiki / memory (stored as markdown in the linked Git repository).
3. **Propose new agenda issues** when it detects an inconsistency or an improvement opportunity (e.g. one repo uses OpenTelemetry while three others still use `Console.WriteLine`).

The agenda project's linked Git repository acts as the **org memory** — a versioned markdown wiki that agents can read and write.

---

## Seed Data Example

The demo organization **Acme Corp** ships with a pre-configured **Common Agenda** project containing:

| # | Title | Status |
|---|-------|--------|
| 1 | Add AI coding skills to all repos | In Progress |
| 2 | Add SAST security scanner to all CI/CD pipelines | Todo |
| 3 | Migrate from deprecated logging library to OpenTelemetry | Backlog |
| 4 | Standardize Dockerfile base images across org | Todo |
| 5 | Enforce branch protection rules on all repos | Done |

Issues 1 and 2 have cross-project links to implementation issues in the **IssuePit** and **Backend API** projects.

---

## Next Steps

- [Configure AI agents →](issupitAgents)
- [Projects overview →](projects)
- [Architecture →](architecture)
