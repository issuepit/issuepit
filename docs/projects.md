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

## GitHub Sync

Each project can be configured to synchronise issues with a GitHub repository. Navigate to **Project Settings → GitHub Sync** to configure.

### Configuration

| Field | Description |
|-------|-------------|
| **GitHub Identity** | A PAT (Personal Access Token) identity from [Config → GitHub Identities](/config/github-identities). Required for all sync operations. |
| **GitHub Repository** | Repository in `owner/repo` format (e.g. `acme/backend`). |
| **Trigger Mode** | `Off` (default) · `Manual` (trigger from the Sync page) · `Auto` (periodic automatic sync). |
| **Auto-Create on GitHub** | When enabled, new issues created in IssuePit are automatically pushed to GitHub. Disabled by default. |

### Importing Issues from GitHub

1. Open **Project Settings → GitHub Sync**.
2. Configure a GitHub identity and repository, then click **Save Configuration**.
3. Click **Trigger Sync Now** to import all open and closed issues from GitHub into this project.

Each GitHub issue is imported only once and linked via `GitHubIssueNumber`. A link to the original GitHub issue appears in the issue sidebar.

### Sync Runs (Audit Log)

Every manual or automatic sync creates a **sync run** record. Open the **Sync Runs** tab to:

- View the status (Pending / Running / Succeeded / Failed) and summary of each run.
- Click **View logs →** to inspect per-line audit output including which issues were imported, updated, or skipped.
- Trigger a new sync from this tab.

### Conflict Detection

The **Conflicts** tab compares linked issues in both systems and lists any where the title or body has diverged. Click **Open in IssuePit →** to resolve the conflict manually.

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

### Issue Preview Sidebar

Clicking any card on the Kanban board opens a **slide-in preview panel** on the right side. The panel shows the issue's status, priority, type, labels, assignees, milestone, and description excerpt. Click **Open Full Issue** to navigate to the full issue page, or click the × button or the backdrop to dismiss the panel.

![Issue preview sidebar](https://github.com/user-attachments/assets/8891f583-cfc1-4612-bd60-6cfbdb42d75a)

### Lane Properties

By default, Kanban columns group issues by **Status**. You can switch the active board's grouping to any of the following lane properties:

| Lane Property | Description |
|---------------|-------------|
| **Status** | Group by issue status (default) |
| **Priority** | Group by priority (Critical, High, Medium, Low, None) |
| **Label** | Group by assigned label |
| **Type** | Group by issue type |
| **Agent** | Group by assigned agent mode |
| **Milestone** | Group by assigned milestone |

To change the lane property, click **New Board** and select the desired **Lane Property**, or view the active board badge in the toolbar to see the current grouping mode. Dragging an issue to a different column automatically updates the corresponding property on that issue.

![Lane property selector and board variants](https://github.com/user-attachments/assets/539cb797-210a-4e57-88a8-5fec1312b37b)

### Lane Transitions

The **Transitions** button in the board toolbar lets you define which column-to-column moves are allowed:

1. Click **Transitions** in the toolbar.
2. Click **Add Transition**.
3. Fill in the **Name**, **From** column, **To** column, and optionally enable **Auto-trigger** (for agent-driven moves).
4. Click **Save**.

When transitions are defined, invalid drop targets are visually grayed out during a drag. When no transitions are defined the board is open — any drag is allowed. The Transitions button pulses amber when dragging from a column that has no outgoing transitions configured.

### Custom Issue Properties

Projects can define **custom properties** to capture structured data beyond the built-in fields (status, priority, type, etc.).

#### Adding a custom property

1. Open your project.
2. Go to **Settings → Custom Properties**.
3. Click **Add Property**.
4. Fill in:
   - **Name** — label shown on the issue form
   - **Type** — one of `Text`, `Number`, `Date`, `Enum`, `Bool`, `Person`, or `Agent`
   - **Required** — whether the field must be filled when creating an issue
   - **Default Value** *(optional)*
   - **Constraints** *(type-specific)*:
     - **Enum** — comma-separated or JSON array of allowed values
     - **Text** — minimum and maximum character length
     - **Number** — minimum and maximum numeric range
     - **Date** — minimum and maximum date
     - **Person / Agent** — allow multiple selections (optionally with a max count)
5. Click **Save**.

![Custom property constraint fields](https://github.com/user-attachments/assets/6bb08cc2-04d4-4bbe-b5b8-4a1bbfec0fe2)

Custom property values are stored per issue and displayed inline in the issue detail view.

---

## Milestones

Milestones let you group issues into time-boxed deliverables and track progress towards a goal.

### Creating a milestone

1. Open your project.
2. Click **Milestones** in the Quick Navigation panel on the project overview, or navigate to the Milestones page from the sidebar.
3. Click **+ New Milestone**.
4. Set a **Title**, optional **Description**, **Start Date**, and **Due Date**.
5. Click **Create Milestone**.

Once a milestone exists, you can assign any issue to it from the issue detail page using the **Milestone** selector.

### List and Gantt views

The milestone list page has three view modes controlled by the toggle in the top-right corner:

| Mode | Description |
|------|-------------|
| **List** | Card-based list of milestones with status, dates, and actions |
| **Both** | List and Gantt chart shown simultaneously (default on larger screens) |
| **Gantt** | Timeline chart only |

![Milestones list and Gantt view](https://github.com/user-attachments/assets/05ea3a1d-6a2e-4295-909a-c78a66b4a7c6)

#### Gantt chart

Each milestone is rendered as a horizontal bar spanning its start–due date range. Open milestones are shown in indigo; closed milestones in gray. A vertical line marks today.

**Interaction:**
- **Click** a bar or its label to open the milestone detail page.
- **Drag** the middle of a bar to shift its start and due dates.
- **Drag** the left or right edge of a bar to resize it (changing only the start or due date).
  All date changes are saved automatically via the API.

### Milestone detail page

Click any milestone row or Gantt bar to open the detail page.

![Milestone detail page](https://github.com/user-attachments/assets/05ea3a1d-6a2e-4295-909a-c78a66b4a7c6)

The detail page shows:
- **Progress bar** with percentage of issues completed
- **Open / In Progress / Done** issue counts
- **Issues table** listing all issues assigned to this milestone
- **Edit** button — opens an inline modal to change the title, description, start date, and due date
- **Close milestone / Reopen milestone** button — toggles the milestone status

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

## Test History

The **Test History** page (`Project → Runs → Test History`) surfaces all test results stored from CI/CD `.trx` artifact files into a queryable dashboard.

![Test history overview](https://github.com/user-attachments/assets/03c2fd1a-b988-4b74-9390-fa7947e6a0b6)

### Tabs

| Tab | Description |
|-----|-------------|
| **Overview** | Summary cards (total / passing / failing / flaky count), a stacked bar chart of pass/fail/skip per run, and a sortable run table |
| **Tests** | Searchable table of all unique tests with a `flaky` badge, fail %, average duration, and last outcome. Click any row for a slide-in panel showing per-run history with error messages and stack traces |
| **Flaky** | Filtered view of tests with mixed results. Use **Create Issue** to pre-fill a new issue with the test name, fail rate, and error context |
| **Compare** | Select two runs (baseline A and comparison B) to see a colour-coded diff: regressed (red), fixed (green), new tests (blue), removed tests (strikethrough), significantly slower tests (yellow) |

### Importing TRX files

Click **Import TRX** (accessible from any tab) to upload a `.trx` file directly — useful for E2E runs that run outside CI. You can optionally specify a commit SHA, branch name, and artifact label.

### MCP tools

Four MCP tools are available for AI-assisted analysis:

| Tool | Description |
|------|-------------|
| `get_test_history` | Run summaries for trend analysis |
| `get_test_list` | All tests sorted by failure count |
| `get_test_case_history` | Per-test flakiness history |
| `compare_test_runs` | Diff two runs: new / removed / fixed / regressed / slower |

---

## Next Steps

- [Configure AI agents →](agents)
- [Set up API keys →](configuration)
- [CI/CD Integration →](cicd)