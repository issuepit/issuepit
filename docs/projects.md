---
title: Projects
layout: default
nav_order: 3
---

# Projects

Projects are the main organizational unit in IssuePit. Each project belongs to an **organization** and can have its own board, issues, milestones, and linked repositories.

---

## Creating a Project

![Projects page]({{ '/assets/screenshots/projects.png' | relative_url }})

1. Navigate to the **Projects** section in the sidebar.
2. Click **New Project**.
3. Fill in the details:
   - **Name** — a short display name (e.g. `my-app`)
   - **Description** *(optional)* — what the project is about

   ![Create Project dialog](https://github.com/user-attachments/assets/0dc2cea0-cc12-4cff-a7cf-4a632239f3fb)

4. Click **Create**.

Your new project appears in the project list immediately.

---

## Git Origins (Multiple Remotes)

A project can have **multiple git origins**, each with a different role:

| Mode | Description |
|------|-------------|
| **Working** | The primary remote. Agents push feature branches and open PRs here. |
| **Read-only** | Fetch-only mirror — never pushed to by agents or the release pipeline. |
| **Release** | Only the default/main branch is pushed here after an agent PR is merged. |

### Adding a Git Origin

1. Open your project.
2. Go to **Settings**.
3. In the **Git Origins** section, click **Add Origin**.
4. Fill in:
   - **Remote URL** — `https://github.com/org/repo.git` or `git@github.com:org/repo.git`
   - **Default branch** — e.g. `main`
   - **Mode** — choose Working, Read-only, or Release
   - **Username** / **Token** — optional credentials for private repositories
5. Click **Add Origin**.

You can add as many origins as needed. The first **Working** remote is used when agents clone the repository.

> **Breaking change (data migration):** Existing single-repository configurations are automatically migrated to `Working` mode.

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

![Kanban board]({{ '/assets/screenshots/kanban.png' | relative_url }})

| Column | Meaning |
|--------|---------|
| Backlog | Not yet scheduled |
| Todo | Scheduled, not started |
| In Progress | Actively being worked on |
| In Review | Ready for code/human review |
| Done | Completed |

To reorder or rename columns, go to **Project Settings → Board**.

---

## Milestones

Milestones let you group issues into time-boxed deliverables and track progress towards a goal.

1. Open your project.
2. Go to **Milestones** in the project sidebar.
3. Click **New Milestone**.
4. Set a **Title**, optional **Description**, and **Due Date**.
5. Click **Create**.

Once a milestone exists, you can assign any issue to it from the issue detail page using the **Milestone** selector.

---

## Sub-issues

Any issue can have child issues (sub-issues) to break large tasks into smaller pieces.

1. Open an issue.
2. Scroll to the **Sub-issues** section.
3. Click **Add Sub-issue**.
4. Search for an existing issue or create a new one inline.

Sub-issues appear indented under their parent and their completion is reflected in a progress indicator on the parent issue.

---

## Issue Linking

Issues can be related to each other using typed links that capture the nature of the relationship.

On any issue detail page:

1. Scroll to **Linked Issues**.
2. Click **Add link**.
3. Choose a **Link type**:

   | Type | Meaning |
   |------|---------|
   | **blocks** | This issue must be resolved before the linked issue can start |
   | **blocked by** | This issue cannot start until the linked issue is resolved |
   | **causes** | This issue is the root cause of the linked issue |
   | **caused by** | This issue is caused by the linked issue |
   | **solves** | This issue provides the solution for the linked issue |
   | **duplicates** | This issue is a duplicate of the linked issue |
   | **requires** | This issue depends on the linked issue |
   | **implements** | This issue implements the requirement in the linked issue |
   | **linked to** | A general relationship with no specific directionality |

4. Search for and select the target issue (cross-project search is supported).
5. Click **Add**.

Links are always shown on both issues and are clearly labelled with a **↗ cross-project** badge when the linked issue belongs to a different project.

---

## Issue History

Every change to an issue is recorded in the **History** tab of the issue detail page. The audit trail includes:

- Status changes
- Assignee additions and removals
- Priority changes
- Title and description edits
- Comments and reactions

Open any issue and switch to the **History** tab to see the full timeline of changes.

---

## Code Review

When a project has a linked Git repository, the **Code Review** tab gives you a side-by-side diff viewer to review changed files between branches.

1. Open your project.
2. Go to **Code Review**.
3. Select a **Base branch** and a **Compare branch** — the diff loads automatically on branch change.
4. Click any file in the sidebar to view its diff.
5. Click any line in the diff to leave an inline comment.

---

## Merge Requests

The **Merge Requests** tab shows lightweight merge request proposals for your linked repository.

1. Open your project.
2. Go to **Merge Requests**.
3. Click **New Merge Request**.
4. Select the **Source branch** and **Target branch**.
5. Add a title and description, then click **Create**.

IssuePit can auto-merge a merge request when all required checks pass.

---

## Runs
{: #runs }

The **Runs** tab shows a combined list of all agent sessions and CI/CD pipeline runs for the project.

- **Agent runs** — show the status and duration of each work agent session
- **CI/CD runs** — show the status of each pipeline execution with a link to the detail view

Click any run to open its detail page with logs, artifacts, and job status.

You can also view runs across all projects from the global **Runs** page in the sidebar.

---

## Next Steps

- [Configure AI agents →](agents)
- [Set up API keys →](configuration)
- [CI/CD Integration →](cicd)