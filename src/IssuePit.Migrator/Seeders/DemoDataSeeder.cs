using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IssuePit.Migrator.Seeders;

public class DemoDataSeeder(IssuePitDbContext db, ILogger<DemoDataSeeder> logger)
{
    public async Task SeedAsync(Guid tenantId)
    {
        await SeedDemoUsersAsync(tenantId);
        await SeedDemoDataAsync(tenantId);
    }

    private async Task SeedDemoUsersAsync(Guid tenantId)
    {
        var demoUsers = new[] { ("alice", "alice@localhost"), ("bob", "bob@localhost") };
        foreach (var (username, email) in demoUsers)
        {
            if (!await db.Users.AnyAsync(u => u.Username == username && u.TenantId == tenantId))
            {
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Username = username,
                    Email = email,
                    CreatedAt = DateTime.UtcNow,
                };
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(username);
                db.Users.Add(user);
                logger.LogInformation("Seeded demo user '{Username}'.", username);
            }
        }
        await db.SaveChangesAsync();
    }

    private async Task SeedDemoDataAsync(Guid tenantId)
    {
        // Only seed demo data once (guard on the demo org slug).
        if (await db.Organizations.AnyAsync(o => o.TenantId == tenantId && o.Slug == "acme"))
            return;

        logger.LogInformation("Seeding demo organization and sample data...");

        // Resolve seeded users for assignees and comments
        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Username == "admin" && u.TenantId == tenantId);
        var aliceUser = await db.Users.FirstOrDefaultAsync(u => u.Username == "alice" && u.TenantId == tenantId);
        var bobUser   = await db.Users.FirstOrDefaultAsync(u => u.Username == "bob"   && u.TenantId == tenantId);

        // --- Organization ---
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Acme Corp",
            Slug = "acme",
            CreatedAt = DateTime.UtcNow,
        };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        // --- Projects ---
        var frontendProject = new Project
        {
            Id = Guid.NewGuid(),
            OrgId = org.Id,
            Name = "Frontend",
            Slug = "frontend",
            Description = "Vue 3 / Nuxt 3 web application",
            //GitHubRepo = "https://github.com/acme/frontend", // disabled
            CreatedAt = DateTime.UtcNow,
        };
        var backendProject = new Project
        {
            Id = Guid.NewGuid(),
            OrgId = org.Id,
            Name = "Backend API",
            Slug = "backend-api",
            Description = "ASP.NET Core REST API",
            //GitHubRepo = "https://github.com/acme/backend", // disabled
            CreatedAt = DateTime.UtcNow,
        };
        db.Projects.AddRange(frontendProject, backendProject);
        await db.SaveChangesAsync();

        // disabled since we have no real repo and do not want to timeout or get out errors
        //db.GitRepositories.AddRange(
        //    new GitRepository { Id = Guid.NewGuid(), ProjectId = frontendProject.Id, RemoteUrl = frontendProject.GitHubRepo!, DefaultBranch = "main", CreatedAt = DateTime.UtcNow },
        //    new GitRepository { Id = Guid.NewGuid(), ProjectId = backendProject.Id, RemoteUrl = backendProject.GitHubRepo!, DefaultBranch = "main", CreatedAt = DateTime.UtcNow }
        //);
        await db.SaveChangesAsync();

        // --- Labels ---
        var labelBug = new Label { Id = Guid.NewGuid(), ProjectId = frontendProject.Id, Name = "bug", Color = "#e11d48" };
        var labelFeature = new Label { Id = Guid.NewGuid(), ProjectId = frontendProject.Id, Name = "feature", Color = "#2563eb" };
        var labelUx = new Label { Id = Guid.NewGuid(), ProjectId = frontendProject.Id, Name = "ux", Color = "#7c3aed" };
        var labelBackend = new Label { Id = Guid.NewGuid(), ProjectId = backendProject.Id, Name = "backend", Color = "#0891b2" };
        var labelPerf = new Label { Id = Guid.NewGuid(), ProjectId = backendProject.Id, Name = "performance", Color = "#d97706" };
        db.Labels.AddRange(labelBug, labelFeature, labelUx, labelBackend, labelPerf);
        await db.SaveChangesAsync();

        // --- Milestones ---
        var milestone = new Milestone
        {
            Id = Guid.NewGuid(),
            ProjectId = frontendProject.Id,
            Title = "v1.0 Launch",
            Description = "Initial public release",
            DueDate = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
        };
        db.Milestones.Add(milestone);
        await db.SaveChangesAsync();

        // --- Kanban boards ---
        var feBoard = new KanbanBoard { Id = Guid.NewGuid(), ProjectId = frontendProject.Id, Name = "Main Board", CreatedAt = DateTime.UtcNow };
        var beBoard = new KanbanBoard { Id = Guid.NewGuid(), ProjectId = backendProject.Id, Name = "Main Board", CreatedAt = DateTime.UtcNow };
        db.KanbanBoards.AddRange(feBoard, beBoard);
        await db.SaveChangesAsync();

        var feColumns = new[]
        {
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = feBoard.Id, Name = "Backlog",     Position = 0, IssueStatus = IssueStatus.Backlog },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = feBoard.Id, Name = "To Do",       Position = 1, IssueStatus = IssueStatus.Todo },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = feBoard.Id, Name = "In Progress", Position = 2, IssueStatus = IssueStatus.InProgress },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = feBoard.Id, Name = "In Review",   Position = 3, IssueStatus = IssueStatus.InReview },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = feBoard.Id, Name = "Done",        Position = 4, IssueStatus = IssueStatus.Done },
        };
        var beColumns = new[]
        {
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = beBoard.Id, Name = "Backlog",     Position = 0, IssueStatus = IssueStatus.Backlog },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = beBoard.Id, Name = "To Do",       Position = 1, IssueStatus = IssueStatus.Todo },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = beBoard.Id, Name = "In Progress", Position = 2, IssueStatus = IssueStatus.InProgress },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = beBoard.Id, Name = "In Review",   Position = 3, IssueStatus = IssueStatus.InReview },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = beBoard.Id, Name = "Done",        Position = 4, IssueStatus = IssueStatus.Done },
        };
        db.KanbanColumns.AddRange(feColumns);
        db.KanbanColumns.AddRange(beColumns);
        await db.SaveChangesAsync();

        // --- Issues (frontend project) — dates spread across the last 14 days ---
        var issues = new[]
        {
            CreateDemoIssue(frontendProject.Id, 1, "Dark mode flicker on page load", "The page briefly shows light mode before switching to dark mode.", IssueStatus.Todo, IssuePriority.High, IssueType.Bug, createdDaysAgo: 13, updatedDaysAgo: 13),
            CreateDemoIssue(frontendProject.Id, 2, "Add keyboard shortcuts for common actions", "Users should be able to navigate and perform actions without a mouse.", IssueStatus.InProgress, IssuePriority.Medium, IssueType.Feature, createdDaysAgo: 11, updatedDaysAgo: 9),
            CreateDemoIssue(frontendProject.Id, 3, "Improve mobile responsiveness on issue detail page", "On small screens, the sidebar overlaps the content area.", IssueStatus.Backlog, IssuePriority.Low, IssueType.Bug, createdDaysAgo: 10, updatedDaysAgo: 10),
            CreateDemoIssue(frontendProject.Id, 4, "Kanban board drag-and-drop support", "Allow dragging issue cards between kanban columns.", IssueStatus.Done, IssuePriority.NoPriority, IssueType.Feature, createdDaysAgo: 14, updatedDaysAgo: 5),
            CreateDemoIssue(frontendProject.Id, 5, "Rich text editor for issue descriptions", "Replace the plain textarea with a Markdown-capable rich text editor.", IssueStatus.InReview, IssuePriority.Medium, IssueType.Feature, createdDaysAgo: 7, updatedDaysAgo: 2),
            // backend project
            CreateDemoIssue(backendProject.Id, 1, "Add rate limiting to public API endpoints", "Prevent abuse by limiting requests per IP address.", IssueStatus.Todo, IssuePriority.Urgent, IssueType.Feature, createdDaysAgo: 12, updatedDaysAgo: 12),
            CreateDemoIssue(backendProject.Id, 2, "Slow query on issue list with many labels", "N+1 query detected when loading issues with labels. Add `.Include()` and index.", IssueStatus.InProgress, IssuePriority.High, IssueType.Bug, createdDaysAgo: 8, updatedDaysAgo: 6),
            CreateDemoIssue(backendProject.Id, 3, "Webhook support for issue state changes", "Allow external systems to subscribe to issue lifecycle events.", IssueStatus.Backlog, IssuePriority.Low, IssueType.Feature, createdDaysAgo: 14, updatedDaysAgo: 14),
        };
        db.Issues.AddRange(issues);
        await db.SaveChangesAsync();

        // Attach labels to frontend/backend issues
        issues[0].Labels.Add(labelBug);
        issues[1].Labels.Add(labelFeature);
        issues[1].Labels.Add(labelUx);
        issues[2].Labels.Add(labelBug);
        issues[2].Labels.Add(labelUx);
        issues[3].Labels.Add(labelFeature);
        issues[4].Labels.Add(labelFeature);
        issues[5].Labels.Add(labelBackend);
        issues[6].Labels.Add(labelBackend);
        issues[6].Labels.Add(labelPerf);
        issues[7].Labels.Add(labelBackend);
        await db.SaveChangesAsync();

        // --- Agents + MCP Servers (delegated to DemoAgentSeeder) ---
        await new DemoAgentSeeder(db).SeedAsync(org.Id);

        // --- IssuePit project ---
        var issuePitProject = new Project
        {
            Id = Guid.NewGuid(),
            OrgId = org.Id,
            Name = "IssuePit",
            Slug = "issuepit",
            Description = "IssuePit — AI-powered issue tracker and agent orchestration platform",
            GitHubRepo = "https://github.com/issuepit/issuepit",
            CreatedAt = DateTime.UtcNow,
        };
        db.Projects.Add(issuePitProject);
        await db.SaveChangesAsync();

        db.GitRepositories.Add(new GitRepository { Id = Guid.NewGuid(), ProjectId = issuePitProject.Id, RemoteUrl = issuePitProject.GitHubRepo!, DefaultBranch = "main", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var ipLabelBug = new Label { Id = Guid.NewGuid(), ProjectId = issuePitProject.Id, Name = "bug", Color = "#e11d48" };
        var ipLabelFeature = new Label { Id = Guid.NewGuid(), ProjectId = issuePitProject.Id, Name = "feature", Color = "#2563eb" };
        var ipLabelEnhancement = new Label { Id = Guid.NewGuid(), ProjectId = issuePitProject.Id, Name = "enhancement", Color = "#0891b2" };
        var ipLabelDocs = new Label { Id = Guid.NewGuid(), ProjectId = issuePitProject.Id, Name = "docs", Color = "#7c3aed" };
        db.Labels.AddRange(ipLabelBug, ipLabelFeature, ipLabelEnhancement, ipLabelDocs);
        await db.SaveChangesAsync();

        var ipBoard = new KanbanBoard { Id = Guid.NewGuid(), ProjectId = issuePitProject.Id, Name = "Main Board", CreatedAt = DateTime.UtcNow };
        db.KanbanBoards.Add(ipBoard);
        await db.SaveChangesAsync();
        db.KanbanColumns.AddRange(
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = ipBoard.Id, Name = "Backlog",     Position = 0, IssueStatus = IssueStatus.Backlog },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = ipBoard.Id, Name = "To Do",       Position = 1, IssueStatus = IssueStatus.Todo },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = ipBoard.Id, Name = "In Progress", Position = 2, IssueStatus = IssueStatus.InProgress },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = ipBoard.Id, Name = "In Review",   Position = 3, IssueStatus = IssueStatus.InReview },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = ipBoard.Id, Name = "Done",        Position = 4, IssueStatus = IssueStatus.Done }
        );
        await db.SaveChangesAsync();

        // IssuePit issues — dates spread across 14 days
        var ipIssue1 = CreateDemoIssue(issuePitProject.Id, 1, "feat: issue editor improvements", "Make the issue page use more screen real estate, add comments, label manager, type changing, assign to user/agent, create sub-issues and tasks.", IssueStatus.InProgress, IssuePriority.High, IssueType.Feature, createdDaysAgo: 13, updatedDaysAgo: 3);
        var ipIssue2 = CreateDemoIssue(issuePitProject.Id, 2, "feat: kanban board drag-and-drop", "Allow dragging issue cards between kanban columns to change their status.", IssueStatus.Todo, IssuePriority.Medium, IssueType.Feature, createdDaysAgo: 11, updatedDaysAgo: 11);
        var ipIssue3 = CreateDemoIssue(issuePitProject.Id, 3, "fix: agent session logs not streaming", "Agent session logs are not streaming in real time via SignalR — only show after completion.", IssueStatus.InProgress, IssuePriority.Urgent, IssueType.Bug, createdDaysAgo: 4, updatedDaysAgo: 1);
        var ipIssue4 = CreateDemoIssue(issuePitProject.Id, 4, "feat: GitHub webhook integration", "Sync issues and PRs from GitHub repositories via webhooks.", IssueStatus.Backlog, IssuePriority.Low, IssueType.Feature, createdDaysAgo: 14, updatedDaysAgo: 14);
        var ipIssue5 = CreateDemoIssue(issuePitProject.Id, 5, "chore: extend seed data", "Add richer seed data including the issuepit/issuepit project itself.", IssueStatus.Done, IssuePriority.NoPriority, IssueType.Task, createdDaysAgo: 7, updatedDaysAgo: 5);
        db.Issues.AddRange(ipIssue1, ipIssue2, ipIssue3, ipIssue4, ipIssue5);
        await db.SaveChangesAsync();

        // Attach labels to some issues
        ipIssue1.Labels.Add(ipLabelFeature);
        ipIssue1.Labels.Add(ipLabelEnhancement);
        ipIssue3.Labels.Add(ipLabelBug);
        ipIssue4.Labels.Add(ipLabelFeature);
        ipIssue5.Labels.Add(ipLabelDocs);
        await db.SaveChangesAsync();

        // Add tasks to ipIssue1
        db.IssueTasks.AddRange(
            new IssueTask { Id = Guid.NewGuid(), IssueId = ipIssue1.Id, Title = "Redesign issue detail page layout", Body = "Use full-width layout and improved sidebar", Status = IssueStatus.Done, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new IssueTask { Id = Guid.NewGuid(), IssueId = ipIssue1.Id, Title = "Add comment functionality", Body = "Allow users to add and delete comments on issues", Status = IssueStatus.InProgress, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new IssueTask { Id = Guid.NewGuid(), IssueId = ipIssue1.Id, Title = "Label manager in sidebar", Body = "Add/remove labels from an issue", Status = IssueStatus.Todo, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new IssueTask { Id = Guid.NewGuid(), IssueId = ipIssue1.Id, Title = "Make issue type editable", Body = "Allow changing the type from the sidebar dropdown", Status = IssueStatus.Todo, CreatedAt = DateTime.UtcNow.AddDays(-1) }
        );
        await db.SaveChangesAsync();

        // --- Common Agenda project ---
        var agendaProject = new Project
        {
            Id = Guid.NewGuid(),
            OrgId = org.Id,
            Name = "Common Agenda",
            Slug = "common-agenda",
            Description = "Org-wide goal tracker — cross-cutting initiatives that span all projects (AI skills, security tooling, library upgrades, CI/CD patterns)",
            IsAgenda = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Projects.Add(agendaProject);
        await db.SaveChangesAsync();

        var agendaLabelAi = new Label { Id = Guid.NewGuid(), ProjectId = agendaProject.Id, Name = "ai", Color = "#7c3aed" };
        var agendaLabelSecurity = new Label { Id = Guid.NewGuid(), ProjectId = agendaProject.Id, Name = "security", Color = "#e11d48" };
        var agendaLabelCiCd = new Label { Id = Guid.NewGuid(), ProjectId = agendaProject.Id, Name = "ci-cd", Color = "#0891b2" };
        var agendaLabelDeps = new Label { Id = Guid.NewGuid(), ProjectId = agendaProject.Id, Name = "dependencies", Color = "#d97706" };
        db.Labels.AddRange(agendaLabelAi, agendaLabelSecurity, agendaLabelCiCd, agendaLabelDeps);
        await db.SaveChangesAsync();

        var agendaBoard = new KanbanBoard { Id = Guid.NewGuid(), ProjectId = agendaProject.Id, Name = "Agenda Board", CreatedAt = DateTime.UtcNow };
        db.KanbanBoards.Add(agendaBoard);
        await db.SaveChangesAsync();
        db.KanbanColumns.AddRange(
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = agendaBoard.Id, Name = "Backlog",     Position = 0, IssueStatus = IssueStatus.Backlog },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = agendaBoard.Id, Name = "To Do",       Position = 1, IssueStatus = IssueStatus.Todo },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = agendaBoard.Id, Name = "In Progress", Position = 2, IssueStatus = IssueStatus.InProgress },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = agendaBoard.Id, Name = "In Review",   Position = 3, IssueStatus = IssueStatus.InReview },
            new KanbanColumn { Id = Guid.NewGuid(), BoardId = agendaBoard.Id, Name = "Done",        Position = 4, IssueStatus = IssueStatus.Done }
        );
        await db.SaveChangesAsync();

        // Agenda issues — dates spread across 14 days, balanced priorities
        var agendaIssue1 = CreateDemoIssue(agendaProject.Id, 1, "Add AI coding skills to all repos", "Configure opencode/copilot agent modes and system prompts for all projects in the org. Each repo should have a plan, code, and evaluate agent mode.", IssueStatus.InProgress, IssuePriority.High, IssueType.Feature, createdDaysAgo: 12, updatedDaysAgo: 4);
        var agendaIssue2 = CreateDemoIssue(agendaProject.Id, 2, "Add SAST security scanner to all CI/CD pipelines", "Integrate a static application security testing (SAST) tool (e.g. Semgrep, CodeQL) into every project's CI/CD workflow.", IssueStatus.Todo, IssuePriority.Urgent, IssueType.Feature, createdDaysAgo: 9, updatedDaysAgo: 9);
        var agendaIssue3 = CreateDemoIssue(agendaProject.Id, 3, "Migrate from deprecated logging library to OpenTelemetry", "The current logging library is end-of-life. Migrate all services to OpenTelemetry for unified observability.", IssueStatus.Backlog, IssuePriority.Medium, IssueType.Task, createdDaysAgo: 14, updatedDaysAgo: 14);
        var agendaIssue4 = CreateDemoIssue(agendaProject.Id, 4, "Standardize Dockerfile base images across org", "All projects should use pinned, minimal base images (e.g. distroless). Document the approved image list.", IssueStatus.Todo, IssuePriority.Low, IssueType.Task, createdDaysAgo: 6, updatedDaysAgo: 6);
        var agendaIssue5 = CreateDemoIssue(agendaProject.Id, 5, "Enforce branch protection rules on all repos", "Main branches on all GitHub repositories should require PR reviews and passing CI before merge.", IssueStatus.Done, IssuePriority.NoPriority, IssueType.Task, createdDaysAgo: 14, updatedDaysAgo: 8);
        db.Issues.AddRange(agendaIssue1, agendaIssue2, agendaIssue3, agendaIssue4, agendaIssue5);
        await db.SaveChangesAsync();

        agendaIssue1.Labels.Add(agendaLabelAi);
        agendaIssue2.Labels.Add(agendaLabelSecurity);
        agendaIssue2.Labels.Add(agendaLabelCiCd);
        agendaIssue3.Labels.Add(agendaLabelDeps);
        agendaIssue4.Labels.Add(agendaLabelCiCd);
        agendaIssue5.Labels.Add(agendaLabelSecurity);
        await db.SaveChangesAsync();

        // Cross-project links: agenda issue 1 → linked to IssuePit project issue; agenda issue 2 → linked to backend issue
        db.IssueLinks.AddRange(
            new IssueLink { Id = Guid.NewGuid(), IssueId = agendaIssue1.Id, TargetIssueId = ipIssue1.Id, LinkType = IssueLinkType.LinkedTo, CreatedAt = DateTime.UtcNow },
            new IssueLink { Id = Guid.NewGuid(), IssueId = agendaIssue2.Id, TargetIssueId = issues[5].Id, LinkType = IssueLinkType.LinkedTo, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        // --- Issue Assignees ---
        if (adminUser is not null || aliceUser is not null || bobUser is not null)
        {
            var assignees = new List<IssueAssignee>();

            // Admin assigned to key IssuePit issues and one from each project
            if (adminUser is not null)
            {
                assignees.Add(new IssueAssignee { Id = Guid.NewGuid(), IssueId = ipIssue1.Id, UserId = adminUser.Id });
                assignees.Add(new IssueAssignee { Id = Guid.NewGuid(), IssueId = ipIssue3.Id, UserId = adminUser.Id });
                assignees.Add(new IssueAssignee { Id = Guid.NewGuid(), IssueId = issues[1].Id, UserId = adminUser.Id }); // FE keyboard shortcuts
                assignees.Add(new IssueAssignee { Id = Guid.NewGuid(), IssueId = agendaIssue1.Id, UserId = adminUser.Id });
            }

            // Alice assigned to frontend and agenda issues
            if (aliceUser is not null)
            {
                assignees.Add(new IssueAssignee { Id = Guid.NewGuid(), IssueId = issues[0].Id, UserId = aliceUser.Id }); // dark mode flicker
                assignees.Add(new IssueAssignee { Id = Guid.NewGuid(), IssueId = issues[4].Id, UserId = aliceUser.Id }); // rich text editor
                assignees.Add(new IssueAssignee { Id = Guid.NewGuid(), IssueId = ipIssue2.Id, UserId = aliceUser.Id });
                assignees.Add(new IssueAssignee { Id = Guid.NewGuid(), IssueId = agendaIssue2.Id, UserId = aliceUser.Id });
            }

            // Bob assigned to backend and remaining issues
            if (bobUser is not null)
            {
                assignees.Add(new IssueAssignee { Id = Guid.NewGuid(), IssueId = issues[5].Id, UserId = bobUser.Id }); // BE rate limiting
                assignees.Add(new IssueAssignee { Id = Guid.NewGuid(), IssueId = issues[6].Id, UserId = bobUser.Id }); // slow query
                assignees.Add(new IssueAssignee { Id = Guid.NewGuid(), IssueId = ipIssue4.Id, UserId = bobUser.Id });
                assignees.Add(new IssueAssignee { Id = Guid.NewGuid(), IssueId = agendaIssue3.Id, UserId = bobUser.Id });
            }

            db.IssueAssignees.AddRange(assignees);
            await db.SaveChangesAsync();
        }

        // --- Issue Comments ---
        var comments = new List<IssueComment>();

        if (aliceUser is not null)
        {
            comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = issues[0].Id, UserId = aliceUser.Id, Body = "Reproduced on Safari 17. The flash happens because the theme class is applied after hydration. We should apply it server-side via a cookie check.", CreatedAt = DateTime.UtcNow.AddDays(-12) });
            comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = issues[6].Id, UserId = aliceUser.Id, Body = "The N+1 is in `IssueListQueryHandler`. We need `.Include(i => i.Labels)` and a composite index on `(project_id, status)`.", CreatedAt = DateTime.UtcNow.AddDays(-7) });
            comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = ipIssue1.Id, UserId = aliceUser.Id, Body = "I've started on the redesign task. Going with a 3-column layout: nav / issue content / metadata sidebar. Should work well on 1280px+.", CreatedAt = DateTime.UtcNow.AddDays(-2) });
            comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = agendaIssue1.Id, UserId = aliceUser.Id, Body = "We already have opencode set up for IssuePit itself. I can use that config as a template for the other repos.", CreatedAt = DateTime.UtcNow.AddDays(-3) });
        }

        if (bobUser is not null)
        {
            comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = issues[0].Id, UserId = bobUser.Id, Body = "Agree with Alice. Alternatively we could use a `<script>` block in `<head>` that reads localStorage and applies the class before React/Vue mounts.", CreatedAt = DateTime.UtcNow.AddDays(-11) });
            comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = issues[5].Id, UserId = bobUser.Id, Body = "I'll implement this using a token bucket algorithm. The rate limits will be configurable via environment variables.", CreatedAt = DateTime.UtcNow.AddDays(-11) });
            comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = issues[6].Id, UserId = bobUser.Id, Body = "Fixed the N+1. Added `.Include()` calls and a migration with the composite index. Tests pass locally. Ready for review.", CreatedAt = DateTime.UtcNow.AddDays(-5) });
            comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = ipIssue3.Id, UserId = bobUser.Id, Body = "The root cause is that the SignalR group name was using the session ID before it was persisted. Moving `AddToGroupAsync` after `SaveChangesAsync` should fix it.", CreatedAt = DateTime.UtcNow.AddDays(-1) });
        }

        if (adminUser is not null)
        {
            comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = ipIssue1.Id, UserId = adminUser.Id, Body = "The comment section should support Markdown rendering. Check how the issue body renderer works and reuse it.", CreatedAt = DateTime.UtcNow.AddDays(-1) });
            comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = ipIssue3.Id, UserId = adminUser.Id, Body = "This is blocking our demo — marking as urgent. @bob please prioritize.", CreatedAt = DateTime.UtcNow.AddDays(-2) });
            comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = agendaIssue2.Id, UserId = adminUser.Id, Body = "CodeQL is already set up for issuepit/issuepit. We should copy the workflow to the other repos as a starting point.", CreatedAt = DateTime.UtcNow.AddDays(-8) });
            comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = issues[5].Id, UserId = adminUser.Id, Body = "Make sure the rate limit headers (`X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`) are included in the response.", CreatedAt = DateTime.UtcNow.AddDays(-10) });
        }

        if (comments.Count > 0)
        {
            db.IssueComments.AddRange(comments);
            await db.SaveChangesAsync();
        }

        // Demo commit SHAs — deliberately fake values for seed data
        const string demoSha1 = "aabbccddeeff00112233445566778899aabbccdd"; // slow-query fix branch
        const string demoSha2 = "1122334455667788990011223344556677889900"; // signalr fix branch

        // --- Code Review Comments ---
        db.CodeReviewComments.AddRange(
            new CodeReviewComment
            {
                Id = Guid.NewGuid(),
                IssueId = issues[6].Id, // slow query fix
                FilePath = "src/IssuePit.Api/QueryHandlers/IssueListQueryHandler.cs",
                StartLine = 42, EndLine = 44,
                Sha = demoSha1,
                Snippet = "    var issues = await db.Issues\n        .Where(i => i.ProjectId == query.ProjectId)\n        .ToListAsync();",
                ContextBefore = "public async Task<List<IssueDto>> Handle(IssueListQuery query, CancellationToken ct)\n{",
                ContextAfter = "    return issues.Select(IssueDto.From).ToList();\n}",
                Body = "**N+1 detected** — labels are loaded lazily after this query. Add `.Include(i => i.Labels)` here to load them in a single query.",
                CreatedAt = DateTime.UtcNow.AddDays(-6),
            },
            new CodeReviewComment
            {
                Id = Guid.NewGuid(),
                IssueId = issues[6].Id,
                FilePath = "src/IssuePit.Core/Migrations/20260210_AddLabelIndex.cs",
                StartLine = 12, EndLine = 14,
                Sha = demoSha1,
                Snippet = "migrationBuilder.CreateIndex(\n    name: \"ix_issues_project_id\",\n    table: \"issues\",",
                ContextBefore = "protected override void Up(MigrationBuilder migrationBuilder)\n{",
                ContextAfter = "    columns: new[] { \"project_id\" });",
                Body = "Consider making this a composite index `(project_id, status)` since the issue list is almost always filtered by both.",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
            },
            new CodeReviewComment
            {
                Id = Guid.NewGuid(),
                IssueId = ipIssue3.Id, // SignalR streaming fix
                FilePath = "src/IssuePit.Api/Hubs/AgentSessionHub.cs",
                StartLine = 28, EndLine = 30,
                Sha = demoSha2,
                Snippet = "await Groups.AddToGroupAsync(Context.ConnectionId, sessionId.ToString());\nawait db.SaveChangesAsync();\nreturn Ok(session);",
                ContextBefore = "var session = new AgentSession { ... };",
                ContextAfter = "",
                Body = "The group join must happen **after** `SaveChangesAsync` — moving it here is the correct fix. Good catch.",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
            }
        );
        await db.SaveChangesAsync();

        // --- Project Metric Snapshots (14 days of daily history for IssuePit and Frontend projects) ---
        // Simulates a project that starts with 4 open issues and gradually moves work through InProgress to Done.
        const int MetricDays = 14;
        var snapshotProjects = new[] { issuePitProject, frontendProject };
        var metricSnapshots = new List<ProjectMetricSnapshot>();
        foreach (var proj in snapshotProjects)
        {
            // One snapshot per day for the last MetricDays days (taken at noon UTC)
            for (var day = MetricDays - 1; day >= 0; day--)
            {
                var recordedAt = DateTime.UtcNow.Date.AddDays(-day).AddHours(12);
                var progress = (MetricDays - 1 - day) / (double)(MetricDays - 1);
                metricSnapshots.Add(new ProjectMetricSnapshot
                {
                    Id = Guid.NewGuid(),
                    ProjectId = proj.Id,
                    RecordedAt = recordedAt,
                    OpenIssues = (int)(4 - progress * 1.5),         // 4 → ~2 open issues over time
                    InProgressIssues = (int)(1 + progress * 2),     // 1 → 3 in-progress over time
                    DoneIssues = (int)(progress * 3),               // 0 → 3 done over time
                    TotalAgentRuns = (int)(progress * 8),           // 0 → 8 agent runs over time
                    TotalCiCdRuns = (int)(progress * 12),           // 0 → 12 CI runs over time
                });
            }
        }
        db.ProjectMetricSnapshots.AddRange(metricSnapshots);
        await db.SaveChangesAsync();

        logger.LogInformation("Demo data seeded: org 'Acme Corp', 4 projects (Frontend, Backend API, IssuePit, Common Agenda), 18 issues, 4 agents, 2 MCP servers, assignees, comments, code review comments, metric snapshots.");
    }

    private static Issue CreateDemoIssue(Guid projectId, int number, string title, string body, IssueStatus status, IssuePriority priority, IssueType type, int createdDaysAgo, int updatedDaysAgo) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Number = number,
            Title = title,
            Body = body,
            Status = status,
            Priority = priority,
            Type = type,
            CreatedAt = DateTime.UtcNow.AddDays(-createdDaysAgo),
            UpdatedAt = DateTime.UtcNow.AddDays(-updatedDaysAgo),
        };
}
