using BCrypt.Net;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();

using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

logger.LogInformation("Ensuring database schema is up to date...");

// Create schema for fresh databases; no-op for existing ones.
await db.Database.EnsureCreatedAsync();

// Apply incremental schema changes for existing databases (idempotent).
await db.Database.ExecuteSqlRawAsync("""
    ALTER TABLE users ADD COLUMN IF NOT EXISTS password_hash text NULL;
    ALTER TABLE users ADD COLUMN IF NOT EXISTS is_admin boolean NOT NULL DEFAULT false;
    CREATE TABLE IF NOT EXISTS telegram_bots (
        id uuid PRIMARY KEY,
        org_id uuid NULL REFERENCES organizations(id) ON DELETE CASCADE,
        project_id uuid NULL REFERENCES projects(id) ON DELETE CASCADE,
        name character varying(200) NOT NULL,
        encrypted_bot_token text NOT NULL,
        chat_id character varying(100) NOT NULL,
        events integer NOT NULL DEFAULT 0,
        is_silent boolean NOT NULL DEFAULT false,
        created_at timestamp with time zone NOT NULL DEFAULT now()
    );
    ALTER TABLE mcp_servers ADD COLUMN IF NOT EXISTS description text NULL;
    ALTER TABLE mcp_servers ADD COLUMN IF NOT EXISTS allowed_tools text NOT NULL DEFAULT '[]';
    CREATE TABLE IF NOT EXISTS issue_comments (
        id uuid PRIMARY KEY,
        issue_id uuid NOT NULL REFERENCES issues(id) ON DELETE CASCADE,
        user_id uuid NULL REFERENCES users(id) ON DELETE SET NULL,
        body text NOT NULL,
        created_at timestamptz NOT NULL DEFAULT now(),
        updated_at timestamptz NOT NULL DEFAULT now()
    );
    CREATE TABLE IF NOT EXISTS mcp_server_secrets (
        id uuid PRIMARY KEY,
        mcp_server_id uuid NOT NULL REFERENCES mcp_servers(id) ON DELETE CASCADE,
        key varchar(200) NOT NULL,
        encrypted_value text NOT NULL,
        created_at timestamptz NOT NULL DEFAULT now()
    );
    CREATE TABLE IF NOT EXISTS mcp_server_projects (
        mcp_server_id uuid NOT NULL REFERENCES mcp_servers(id) ON DELETE CASCADE,
        project_id uuid NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
        PRIMARY KEY (mcp_server_id, project_id)
    );
    """);

logger.LogInformation("Schema applied successfully.");

logger.LogInformation("Running database seed...");
await SeedAsync(db, logger);
logger.LogInformation("Seed completed.");

static async Task SeedAsync(IssuePitDbContext db, ILogger logger)
{
    if (!await db.Tenants.AnyAsync())
    {
        var tenant = new IssuePit.Core.Entities.Tenant
        {
            Id = Guid.NewGuid(),
            Hostname = "localhost",
            Name = "Default Tenant",
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded default tenant.");
    }

    var defaultTenant = await db.Tenants.FirstAsync(t => t.Hostname == "localhost");

    if (!await db.Users.AnyAsync(u => u.Username == "admin" && u.TenantId == defaultTenant.Id))
    {
        var admin = new User
        {
            Id = Guid.NewGuid(),
            TenantId = defaultTenant.Id,
            Username = "admin",
            Email = "admin@localhost",
            IsAdmin = true,
            CreatedAt = DateTime.UtcNow,
        };
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin");
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded default admin user (admin/admin).");
    }

    await SeedDemoDataAsync(db, defaultTenant.Id, logger);
}

static async Task SeedDemoDataAsync(IssuePitDbContext db, Guid tenantId, ILogger logger)
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
        GitHubRepo = "https://github.com/acme/frontend",
        CreatedAt = DateTime.UtcNow,
    };
    var backendProject = new Project
    {
        Id = Guid.NewGuid(),
        OrgId = org.Id,
        Name = "Backend API",
        Slug = "backend-api",
        Description = "ASP.NET Core REST API",
        GitHubRepo = "https://github.com/acme/backend",
        CreatedAt = DateTime.UtcNow,
    };
    db.Projects.AddRange(frontendProject, backendProject);
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

    logger.LogInformation("Demo data seeded: org 'Acme Corp', 3 projects (frontend, backend, IssuePit), 13 issues, 3 agents, 2 MCP servers.");
}

static Issue CreateDemoIssue(Guid projectId, int number, string title, string body, IssueStatus status, IssuePriority priority, IssueType type, int daysAgo) =>
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
