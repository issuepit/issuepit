---
title: Projects
layout: default
nav_order: 3
---

# Projects

Projects are the main organizational unit in IssuePit. Each project belongs to an **organization** and can have its own board, issues, milestones, and linked repositories.

---

## Creating a Project

![Projects page](https://github.com/user-attachments/assets/7dc21998-66e4-40c3-aefe-57e81efeffca)

1. Navigate to the **Projects** section in the sidebar.
2. Click **New Project**.
3. Fill in the details:
   - **Name** — a short display name (e.g. `my-app`)
   - **Description** *(optional)* — what the project is about

   ![Create Project dialog](https://github.com/user-attachments/assets/0dc2cea0-cc12-4cff-a7cf-4a632239f3fb)

4. Click **Create**.

Your new project appears in the project list immediately.

---

## Linking a Git Repository

Linking a repository allows IssuePit to import issues from GitHub and create branches on your behalf.

1. Open your project.
2. Go to **Settings** (the gear icon or project settings tab).
3. Under **Repository**, enter the repository URL in the format:
   ```
   https://github.com/<owner>/<repo>
   ```
   or for self-hosted GitLab:
   ```
   https://gitlab.example.com/<owner>/<repo>
   ```
4. Click **Save**.

> **Note:** To import issues or trigger actions on GitHub, you also need to add a GitHub API key in [Configuration → API Keys](configuration#api-keys).

---

## Importing Issues from GitHub

Once a repository is linked, you can import individual issues by their GitHub issue number:

1. Open the project's **Issues** tab.
2. Click **Import from GitHub**.
3. Enter the GitHub issue number.
4. Click **Import** — the issue content, labels, and description are copied into IssuePit.

---

## Managing Boards

Each project has a **Kanban board** with the following columns by default:

| Column | Meaning |
|--------|---------|
| Backlog | Not yet scheduled |
| Todo | Scheduled, not started |
| In Progress | Actively being worked on |
| In Review | Ready for code/human review |
| Done | Completed |

To reorder or rename columns, go to **Project Settings → Board**.

---

## Next Steps

- [Configure AI agents →](agents)
- [Set up API keys →](configuration)
