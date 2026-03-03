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

        // --- Issues (frontend project) ---
        var issues = new[]
        {
            CreateDemoIssue(frontendProject.Id, 1, "Dark mode flicker on page load", "The page briefly shows light mode before switching to dark mode.", IssueStatus.Todo, IssuePriority.High, IssueType.Bug, 10),
            CreateDemoIssue(frontendProject.Id, 2, "Add keyboard shortcuts for common actions", "Users should be able to navigate and perform actions without a mouse.", IssueStatus.InProgress, IssuePriority.Medium, IssueType.Feature, 8),
            CreateDemoIssue(frontendProject.Id, 3, "Improve mobile responsiveness on issue detail page", "On small screens, the sidebar overlaps the content area.", IssueStatus.Backlog, IssuePriority.Low, IssueType.Bug, 5),
            CreateDemoIssue(frontendProject.Id, 4, "Kanban board drag-and-drop support", "Allow dragging issue cards between kanban columns.", IssueStatus.Done, IssuePriority.High, IssueType.Feature, 20),
            CreateDemoIssue(frontendProject.Id, 5, "Rich text editor for issue descriptions", "Replace the plain textarea with a Markdown-capable rich text editor.", IssueStatus.InReview, IssuePriority.Medium, IssueType.Feature, 3),
            // backend project
            CreateDemoIssue(backendProject.Id, 1, "Add rate limiting to public API endpoints", "Prevent abuse by limiting requests per IP address.", IssueStatus.Todo, IssuePriority.High, IssueType.Feature, 7),
            CreateDemoIssue(backendProject.Id, 2, "Slow query on issue list with many labels", "N+1 query detected when loading issues with labels. Add `.Include()` and index.", IssueStatus.InProgress, IssuePriority.Urgent, IssueType.Bug, 2),
            CreateDemoIssue(backendProject.Id, 3, "Webhook support for issue state changes", "Allow external systems to subscribe to issue lifecycle events.", IssueStatus.Backlog, IssuePriority.Medium, IssueType.Feature, 15),
        };
        db.Issues.AddRange(issues);
        await db.SaveChangesAsync();

        // --- MCP Servers ---
        var mcpGitHub = new McpServer
        {
            Id = Guid.NewGuid(),
            OrgId = org.Id,
            Name = "GitHub MCP",
            Url = "https://mcp.example.com/github",
            Configuration = "{}",
            CreatedAt = DateTime.UtcNow,
        };
        var mcpFilesystem = new McpServer
        {
            Id = Guid.NewGuid(),
            OrgId = org.Id,
            Name = "Filesystem MCP",
            Url = "https://mcp.example.com/filesystem",
            Configuration = "{}",
            CreatedAt = DateTime.UtcNow,
        };
        db.McpServers.AddRange(mcpGitHub, mcpFilesystem);
        await db.SaveChangesAsync();

        // --- Agents ---
        var planAgent = new Agent
        {
            Id = Guid.NewGuid(),
            OrgId = org.Id,
            Name = "Plan Agent",
            SystemPrompt = "You are a senior software architect. Analyze the assigned issue and create a detailed implementation plan with subtasks. Be concise and actionable.",
            DockerImage = "ghcr.io/sst/opencode:latest",
            AllowedTools = "[]",
            CreatedAt = DateTime.UtcNow,
        };
        var codeAgent = new Agent
        {
            Id = Guid.NewGuid(),
            OrgId = org.Id,
            Name = "Code Agent",
            SystemPrompt = "You are a senior full-stack developer. Implement the described issue following existing code conventions. Create a conventional commit. Do not modify unrelated files.",
            DockerImage = "ghcr.io/sst/opencode:latest",
            AllowedTools = "[]",
            CreatedAt = DateTime.UtcNow,
        };
        var evalAgent = new Agent
        {
            Id = Guid.NewGuid(),
            OrgId = org.Id,
            Name = "Evaluate Agent",
            SystemPrompt = "You are a code reviewer. Review the changes made for the assigned issue. Check for correctness, code style, tests, and security issues. Leave concise feedback.",
            DockerImage = "ghcr.io/sst/opencode:latest",
            AllowedTools = "[]",
            CreatedAt = DateTime.UtcNow,
        };
        db.Agents.AddRange(planAgent, codeAgent, evalAgent);
        await db.SaveChangesAsync();

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

        var ipIssue1 = CreateDemoIssue(issuePitProject.Id, 1, "feat: issue editor improvements", "Make the issue page use more screen real estate, add comments, label manager, type changing, assign to user/agent, create sub-issues and tasks.", IssueStatus.InProgress, IssuePriority.High, IssueType.Feature, 3);
        var ipIssue2 = CreateDemoIssue(issuePitProject.Id, 2, "feat: kanban board drag-and-drop", "Allow dragging issue cards between kanban columns to change their status.", IssueStatus.Todo, IssuePriority.High, IssueType.Feature, 5);
        var ipIssue3 = CreateDemoIssue(issuePitProject.Id, 3, "fix: agent session logs not streaming", "Agent session logs are not streaming in real time via SignalR — only show after completion.", IssueStatus.InProgress, IssuePriority.Urgent, IssueType.Bug, 1);
        var ipIssue4 = CreateDemoIssue(issuePitProject.Id, 4, "feat: GitHub webhook integration", "Sync issues and PRs from GitHub repositories via webhooks.", IssueStatus.Backlog, IssuePriority.Medium, IssueType.Feature, 10);
        var ipIssue5 = CreateDemoIssue(issuePitProject.Id, 5, "chore: extend seed data", "Add richer seed data including the issuepit/issuepit project itself.", IssueStatus.Done, IssuePriority.Low, IssueType.Task, 2);
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

        var agendaIssue1 = CreateDemoIssue(agendaProject.Id, 1, "Add AI coding skills to all repos", "Configure opencode/copilot agent modes and system prompts for all projects in the org. Each repo should have a plan, code, and evaluate agent mode.", IssueStatus.InProgress, IssuePriority.High, IssueType.Feature, 5);
        var agendaIssue2 = CreateDemoIssue(agendaProject.Id, 2, "Add SAST security scanner to all CI/CD pipelines", "Integrate a static application security testing (SAST) tool (e.g. Semgrep, CodeQL) into every project's CI/CD workflow.", IssueStatus.Todo, IssuePriority.High, IssueType.Feature, 3);
        var agendaIssue3 = CreateDemoIssue(agendaProject.Id, 3, "Migrate from deprecated logging library to OpenTelemetry", "The current logging library is end-of-life. Migrate all services to OpenTelemetry for unified observability.", IssueStatus.Backlog, IssuePriority.Medium, IssueType.Task, 7);
        var agendaIssue4 = CreateDemoIssue(agendaProject.Id, 4, "Standardize Dockerfile base images across org", "All projects should use pinned, minimal base images (e.g. distroless). Document the approved image list.", IssueStatus.Todo, IssuePriority.Medium, IssueType.Task, 2);
        var agendaIssue5 = CreateDemoIssue(agendaProject.Id, 5, "Enforce branch protection rules on all repos", "Main branches on all GitHub repositories should require PR reviews and passing CI before merge.", IssueStatus.Done, IssuePriority.Urgent, IssueType.Task, 14);
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

        logger.LogInformation("Demo data seeded: org 'Acme Corp', 4 projects (Frontend, Backend API, IssuePit, Common Agenda), 18 issues, 3 agents, 2 MCP servers.");
    }

    private static Issue CreateDemoIssue(Guid projectId, int number, string title, string body, IssueStatus status, IssuePriority priority, IssueType type, int daysAgo) =>
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
            CreatedAt = DateTime.UtcNow.AddDays(-daysAgo),
        };
}
