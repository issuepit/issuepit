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
        await SeedEvilCorpAsync(tenantId);
        await SeedDemoTodosAsync(tenantId);
    }

    private async Task SeedDemoUsersAsync(Guid tenantId)
    {
        var demoUsers = new[]
        {
            // Honest / generic participants (cryptographic naming convention)
            ("alice",   "alice@localhost",   "The sender who initiates communication."),
            ("bob",     "bob@localhost",     "The intended recipient."),
            ("carol",   "carol@localhost",   "A third honest participant."),
            ("caesar",  "caesar@localhost",  "Named after the Caesar cipher; an early cryptographer."),
            ("dave",    "dave@localhost",    "A fourth participant, sometimes called Dan."),
            ("erin",    "erin@localhost",    "Occasionally used as an additional honest participant."),
            ("frank",   "frank@localhost",   "Another generic participant."),
            ("grace",   "grace@localhost",   "Sometimes used as an additional user."),
            ("heidi",   "heidi@localhost",   "Common extra participant in protocol examples."),
            ("ivan",    "ivan@localhost",    "Additional honest participant."),
            ("judy",    "judy@localhost",    "A judge or dispute resolver."),
            ("joe",     "joe@localhost",     "Generic user in examples."),
            ("niaj",    "niaj@localhost",    "Generic participant (used in RFC examples)."),
            ("olivia",  "olivia@localhost",  "Honest participant (contrast with Oscar)."),
            ("peggy",   "peggy@localhost",   "The Prover in zero-knowledge proofs."),
            ("rupert",  "rupert@localhost",  "Occasionally used as another participant."),
            ("sybil",   "sybil@localhost",   "Used both as participant and attacker (Sybil attack)."),
            ("trent",   "trent@localhost",   "Trusted third party / authority."),
            ("victor",  "victor@localhost",  "The Verifier in zero-knowledge proofs."),
            ("walter",  "walter@localhost",  "The Warden monitoring communication."),
            // Attackers
            ("eve",       "eve@localhost",       "Passive eavesdropper."),
            ("mallory",   "mallory@localhost",   "Active malicious attacker (MITM)."),
            ("malice",    "malice@localhost",    "Personification of malicious intent."),
            ("trudy",     "trudy@localhost",     "Active intruder (similar to Mallory)."),
            ("oscar",     "oscar@localhost",     "Opponent / outsider."),
            ("hackerman", "hackerman@localhost",  "Generic attacker label (less formal)."),
            // Theoretical / proof-system participants
            ("arthur",  "arthur@localhost",  "Polynomial-time verifier in interactive proofs."),
            ("merlin",  "merlin@localhost",  "All-powerful prover in complexity theory."),
            ("zeke",    "zeke@localhost",    "Occasionally used in academic protocol examples."),
        };
        foreach (var (username, email, description) in demoUsers)
        {
            if (!await db.Users.AnyAsync(u => u.Username == username && u.TenantId == tenantId))
            {
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Username = username,
                    Email = email,
                    Description = description,
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
        logger.LogInformation("Seeding demo organization and sample data...");

        // Resolve seeded users for assignees and comments
        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Username == "admin" && u.TenantId == tenantId);
        var aliceUser = await db.Users.FirstOrDefaultAsync(u => u.Username == "alice" && u.TenantId == tenantId);
        var bobUser   = await db.Users.FirstOrDefaultAsync(u => u.Username == "bob"   && u.TenantId == tenantId);

        // --- Organization ---
        var (org, _) = await db.Organizations.AddIfNotExistsAsync(
            o => o.TenantId == tenantId && o.Slug == "acme",
            new Organization
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Acme Corp",
                Slug = "acme",
                // Use the custom runner image built for this environment.
                ActRunnerImage = "ghcr.io/catthehacker/ubuntu:custom-24.04",
                CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        // --- Projects ---
        var (frontendProject, _) = await db.Projects.AddIfNotExistsAsync(
            p => p.OrgId == org.Id && p.Slug == "frontend",
            new Project
            {
                Id = Guid.NewGuid(),
                OrgId = org.Id,
                Name = "Frontend",
                Slug = "frontend",
                Description = "Vue 3 / Nuxt 3 web application",
                IssueKey = "FE",
                //GitHubRepo = "https://github.com/acme/frontend", // disabled
                CreatedAt = DateTime.UtcNow,
            });
        var (backendProject, _) = await db.Projects.AddIfNotExistsAsync(
            p => p.OrgId == org.Id && p.Slug == "backend-api",
            new Project
            {
                Id = Guid.NewGuid(),
                OrgId = org.Id,
                Name = "Backend API",
                Slug = "backend-api",
                Description = "ASP.NET Core REST API",
                IssueKey = "BA",
                //GitHubRepo = "https://github.com/acme/backend", // disabled
                CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        // disabled since we have no real repo and do not want to timeout or get out errors
        //db.GitRepositories.AddRange(
        //    new GitRepository { Id = Guid.NewGuid(), ProjectId = frontendProject.Id, RemoteUrl = frontendProject.GitHubRepo!, DefaultBranch = "main", CreatedAt = DateTime.UtcNow },
        //    new GitRepository { Id = Guid.NewGuid(), ProjectId = backendProject.Id, RemoteUrl = backendProject.GitHubRepo!, DefaultBranch = "main", CreatedAt = DateTime.UtcNow }
        //);

        // --- Labels ---
        var (labelBug, _)     = await db.Labels.AddIfNotExistsAsync(l => l.ProjectId == frontendProject.Id && l.Name == "bug",         new Label { Id = Guid.NewGuid(), ProjectId = frontendProject.Id, Name = "bug",         Color = "#e11d48" });
        var (labelFeature, _) = await db.Labels.AddIfNotExistsAsync(l => l.ProjectId == frontendProject.Id && l.Name == "feature",     new Label { Id = Guid.NewGuid(), ProjectId = frontendProject.Id, Name = "feature",     Color = "#2563eb" });
        var (labelUx, _)      = await db.Labels.AddIfNotExistsAsync(l => l.ProjectId == frontendProject.Id && l.Name == "ux",          new Label { Id = Guid.NewGuid(), ProjectId = frontendProject.Id, Name = "ux",          Color = "#7c3aed" });
        var (labelBackend, _) = await db.Labels.AddIfNotExistsAsync(l => l.ProjectId == backendProject.Id  && l.Name == "backend",     new Label { Id = Guid.NewGuid(), ProjectId = backendProject.Id,  Name = "backend",     Color = "#0891b2" });
        var (labelPerf, _)    = await db.Labels.AddIfNotExistsAsync(l => l.ProjectId == backendProject.Id  && l.Name == "performance", new Label { Id = Guid.NewGuid(), ProjectId = backendProject.Id,  Name = "performance", Color = "#d97706" });
        await db.SaveChangesAsync();

        // --- Milestones ---
        var (feMilestone, _) = await db.Milestones.AddIfNotExistsAsync(m => m.ProjectId == frontendProject.Id && m.Title == "v1.0 Launch",
            new Milestone { Id = Guid.NewGuid(), ProjectId = frontendProject.Id, Title = "v1.0 Launch", Description = "Initial public release", StartDate = DateTime.UtcNow.AddDays(-14), DueDate = DateTime.UtcNow.AddDays(30), CreatedAt = DateTime.UtcNow.AddDays(-20) });
        var (beMilestoneHardening, _) = await db.Milestones.AddIfNotExistsAsync(m => m.ProjectId == backendProject.Id && m.Title == "API Hardening",
            new Milestone { Id = Guid.NewGuid(), ProjectId = backendProject.Id, Title = "API Hardening", Description = "Rate limiting, input validation, and security hardening", StartDate = DateTime.UtcNow.AddDays(-7), DueDate = DateTime.UtcNow.AddDays(14), CreatedAt = DateTime.UtcNow.AddDays(-7) });
        var (beMilestoneObs, _) = await db.Milestones.AddIfNotExistsAsync(m => m.ProjectId == backendProject.Id && m.Title == "Observability",
            new Milestone { Id = Guid.NewGuid(), ProjectId = backendProject.Id, Title = "Observability", Description = "Structured logging, health checks, and correlation IDs", StartDate = DateTime.UtcNow.AddDays(14), DueDate = DateTime.UtcNow.AddDays(45), Status = MilestoneStatus.Open, CreatedAt = DateTime.UtcNow.AddDays(-3) });
        await db.SaveChangesAsync();

        // --- Kanban boards ---
        var (feBoard, _) = await db.KanbanBoards.AddIfNotExistsAsync(b => b.ProjectId == frontendProject.Id && b.Name == "Main Board", new KanbanBoard { Id = Guid.NewGuid(), ProjectId = frontendProject.Id, Name = "Main Board", CreatedAt = DateTime.UtcNow });
        var (beBoard, _) = await db.KanbanBoards.AddIfNotExistsAsync(b => b.ProjectId == backendProject.Id  && b.Name == "Main Board", new KanbanBoard { Id = Guid.NewGuid(), ProjectId = backendProject.Id,  Name = "Main Board", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == feBoard.Id && c.IssueStatus == IssueStatus.Backlog,    new KanbanColumn { Id = Guid.NewGuid(), BoardId = feBoard.Id, Name = "Backlog",     Position = 0, IssueStatus = IssueStatus.Backlog });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == feBoard.Id && c.IssueStatus == IssueStatus.Todo,       new KanbanColumn { Id = Guid.NewGuid(), BoardId = feBoard.Id, Name = "To Do",       Position = 1, IssueStatus = IssueStatus.Todo });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == feBoard.Id && c.IssueStatus == IssueStatus.InProgress, new KanbanColumn { Id = Guid.NewGuid(), BoardId = feBoard.Id, Name = "In Progress", Position = 2, IssueStatus = IssueStatus.InProgress });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == feBoard.Id && c.IssueStatus == IssueStatus.InReview,   new KanbanColumn { Id = Guid.NewGuid(), BoardId = feBoard.Id, Name = "In Review",   Position = 3, IssueStatus = IssueStatus.InReview });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == feBoard.Id && c.IssueStatus == IssueStatus.Done,       new KanbanColumn { Id = Guid.NewGuid(), BoardId = feBoard.Id, Name = "Done",        Position = 4, IssueStatus = IssueStatus.Done });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == beBoard.Id && c.IssueStatus == IssueStatus.Backlog,    new KanbanColumn { Id = Guid.NewGuid(), BoardId = beBoard.Id, Name = "Backlog",     Position = 0, IssueStatus = IssueStatus.Backlog });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == beBoard.Id && c.IssueStatus == IssueStatus.Todo,       new KanbanColumn { Id = Guid.NewGuid(), BoardId = beBoard.Id, Name = "To Do",       Position = 1, IssueStatus = IssueStatus.Todo });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == beBoard.Id && c.IssueStatus == IssueStatus.InProgress, new KanbanColumn { Id = Guid.NewGuid(), BoardId = beBoard.Id, Name = "In Progress", Position = 2, IssueStatus = IssueStatus.InProgress });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == beBoard.Id && c.IssueStatus == IssueStatus.InReview,   new KanbanColumn { Id = Guid.NewGuid(), BoardId = beBoard.Id, Name = "In Review",   Position = 3, IssueStatus = IssueStatus.InReview });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == beBoard.Id && c.IssueStatus == IssueStatus.Done,       new KanbanColumn { Id = Guid.NewGuid(), BoardId = beBoard.Id, Name = "Done",        Position = 4, IssueStatus = IssueStatus.Done });
        await db.SaveChangesAsync();

        // --- Issues (frontend project) — dates spread across the last 14 days ---
        var (feIssue1, feIssue1IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == frontendProject.Id && i.Number == 1, CreateDemoIssue(frontendProject.Id, 1, "Dark mode flicker on page load", "The page briefly shows light mode before switching to dark mode.", IssueStatus.Todo, IssuePriority.High, IssueType.Bug, createdDaysAgo: 13, updatedDaysAgo: 13));
        var (feIssue2, feIssue2IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == frontendProject.Id && i.Number == 2, CreateDemoIssue(frontendProject.Id, 2, "Add keyboard shortcuts for common actions", "Users should be able to navigate and perform actions without a mouse.", IssueStatus.InProgress, IssuePriority.Medium, IssueType.Feature, createdDaysAgo: 11, updatedDaysAgo: 9));
        var (feIssue3, feIssue3IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == frontendProject.Id && i.Number == 3, CreateDemoIssue(frontendProject.Id, 3, "Improve mobile responsiveness on issue detail page", "On small screens, the sidebar overlaps the content area.", IssueStatus.Backlog, IssuePriority.Low, IssueType.Bug, createdDaysAgo: 10, updatedDaysAgo: 10));
        var (feIssue4, feIssue4IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == frontendProject.Id && i.Number == 4, CreateDemoIssue(frontendProject.Id, 4, "Kanban board drag-and-drop support", "Allow dragging issue cards between kanban columns.", IssueStatus.Done, IssuePriority.NoPriority, IssueType.Feature, createdDaysAgo: 14, updatedDaysAgo: 5));
        var (feIssue5, feIssue5IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == frontendProject.Id && i.Number == 5, CreateDemoIssue(frontendProject.Id, 5, "Rich text editor for issue descriptions", "Replace the plain textarea with a Markdown-capable rich text editor.", IssueStatus.InReview, IssuePriority.Medium, IssueType.Feature, createdDaysAgo: 7, updatedDaysAgo: 2));
        var (feIssue6, feIssue6IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == frontendProject.Id && i.Number == 6, CreateDemoIssue(frontendProject.Id, 6, "Add pagination to issue list", "The issue list loads all issues at once. Add server-side pagination to handle large projects.", IssueStatus.Backlog, IssuePriority.Medium, IssueType.Feature, createdDaysAgo: 9, updatedDaysAgo: 9));
        var (feIssue7, _)            = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == frontendProject.Id && i.Number == 7, CreateDemoIssue(frontendProject.Id, 7, "Tooltip for truncated issue titles", "Long titles get cut off in the list view without any way to read the full text.", IssueStatus.Backlog, IssuePriority.Low, IssueType.Bug, createdDaysAgo: 6, updatedDaysAgo: 6));
        var (feIssue8, feIssue8IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == frontendProject.Id && i.Number == 8, CreateDemoIssue(frontendProject.Id, 8, "Loading skeleton for issue cards", "Replace the spinner with a proper skeleton screen while issues load.", IssueStatus.Todo, IssuePriority.Low, IssueType.Feature, createdDaysAgo: 4, updatedDaysAgo: 4));

        // --- Issues (backend project) ---
        var (beIssue1, beIssue1IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == backendProject.Id && i.Number == 1, CreateDemoIssue(backendProject.Id, 1, "Add rate limiting to public API endpoints", "Prevent abuse by limiting requests per IP address.", IssueStatus.Todo, IssuePriority.Urgent, IssueType.Feature, createdDaysAgo: 12, updatedDaysAgo: 12));
        var (beIssue2, beIssue2IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == backendProject.Id && i.Number == 2, CreateDemoIssue(backendProject.Id, 2, "Slow query on issue list with many labels", "N+1 query detected when loading issues with labels. Add `.Include()` and index.", IssueStatus.InProgress, IssuePriority.High, IssueType.Bug, createdDaysAgo: 8, updatedDaysAgo: 6));
        var (beIssue3, beIssue3IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == backendProject.Id && i.Number == 3, CreateDemoIssue(backendProject.Id, 3, "Webhook support for issue state changes", "Allow external systems to subscribe to issue lifecycle events.", IssueStatus.Backlog, IssuePriority.Low, IssueType.Feature, createdDaysAgo: 14, updatedDaysAgo: 14));
        var (beIssue4, beIssue4IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == backendProject.Id && i.Number == 4, CreateDemoIssue(backendProject.Id, 4, "Add OpenAPI / Swagger documentation", "Generate interactive API docs from controller attributes.", IssueStatus.Todo, IssuePriority.Medium, IssueType.Feature, createdDaysAgo: 11, updatedDaysAgo: 11));
        var (beIssue5, beIssue5IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == backendProject.Id && i.Number == 5, CreateDemoIssue(backendProject.Id, 5, "Health-check endpoint for k8s probes", "Expose `/healthz` for liveness and `/readyz` for readiness probes.", IssueStatus.Done, IssuePriority.Medium, IssueType.Feature, createdDaysAgo: 13, updatedDaysAgo: 7));
        var (beIssue6, beIssue6IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == backendProject.Id && i.Number == 6, CreateDemoIssue(backendProject.Id, 6, "Structured logging with correlation IDs", "Add request correlation IDs to all log entries to aid debugging.", IssueStatus.Backlog, IssuePriority.Low, IssueType.Task, createdDaysAgo: 5, updatedDaysAgo: 5));
        var (beIssue7, _)            = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == backendProject.Id && i.Number == 7, CreateDemoIssue(backendProject.Id, 7, "Validate input DTOs with FluentValidation", "Replace manual validation checks with a consistent FluentValidation pipeline.", IssueStatus.Backlog, IssuePriority.Low, IssueType.Task, createdDaysAgo: 3, updatedDaysAgo: 3));
        await db.SaveChangesAsync();

        // Attach labels to newly created frontend/backend issues
        if (feIssue1IsNew) feIssue1.Labels.Add(labelBug);
        if (feIssue2IsNew) { feIssue2.Labels.Add(labelFeature); feIssue2.Labels.Add(labelUx); }
        if (feIssue3IsNew) { feIssue3.Labels.Add(labelBug); feIssue3.Labels.Add(labelUx); }
        if (feIssue4IsNew) feIssue4.Labels.Add(labelFeature);
        if (feIssue5IsNew) feIssue5.Labels.Add(labelFeature);
        if (feIssue6IsNew) feIssue6.Labels.Add(labelFeature);
        if (feIssue8IsNew) feIssue8.Labels.Add(labelUx);
        if (beIssue1IsNew) beIssue1.Labels.Add(labelBackend);
        if (beIssue2IsNew) { beIssue2.Labels.Add(labelBackend); beIssue2.Labels.Add(labelPerf); }
        if (beIssue3IsNew) beIssue3.Labels.Add(labelBackend);
        if (beIssue4IsNew) beIssue4.Labels.Add(labelBackend);
        if (beIssue5IsNew) beIssue5.Labels.Add(labelBackend);
        await db.SaveChangesAsync();

        // Assign issues to milestones (idempotent: only set when MilestoneId is currently null)
        if (feIssue1.MilestoneId is null) { feIssue1.MilestoneId = feMilestone.Id; }
        if (feIssue2.MilestoneId is null) { feIssue2.MilestoneId = feMilestone.Id; }
        if (feIssue3.MilestoneId is null) { feIssue3.MilestoneId = feMilestone.Id; }
        if (feIssue4.MilestoneId is null) { feIssue4.MilestoneId = feMilestone.Id; }
        if (feIssue5.MilestoneId is null) { feIssue5.MilestoneId = feMilestone.Id; }
        if (feIssue6.MilestoneId is null) { feIssue6.MilestoneId = feMilestone.Id; }
        if (beIssue1.MilestoneId is null) { beIssue1.MilestoneId = beMilestoneHardening.Id; }
        if (beIssue2.MilestoneId is null) { beIssue2.MilestoneId = beMilestoneHardening.Id; }
        if (beIssue4.MilestoneId is null) { beIssue4.MilestoneId = beMilestoneHardening.Id; }
        if (beIssue5.MilestoneId is null) { beIssue5.MilestoneId = beMilestoneObs.Id; }
        if (beIssue6.MilestoneId is null) { beIssue6.MilestoneId = beMilestoneObs.Id; }
        if (beIssue7.MilestoneId is null) { beIssue7.MilestoneId = beMilestoneObs.Id; }
        await db.SaveChangesAsync();

        // --- Agents + MCP Servers (delegated to DemoAgentSeeder) ---
        await new DemoAgentSeeder(db).SeedAsync(org.Id);

        // --- IssuePit project ---
        var (issuePitProject, _) = await db.Projects.AddIfNotExistsAsync(
            p => p.OrgId == org.Id && p.Slug == "issuepit",
            new Project
            {
                Id = Guid.NewGuid(),
                OrgId = org.Id,
                Name = "IssuePit",
                Slug = "issuepit",
                Description = "IssuePit — AI-powered issue tracker and agent orchestration platform",
                GitHubRepo = "https://github.com/issuepit/issuepit",
                IssueKey = "IP",
                CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        if (!await db.GitRepositories.AnyAsync(r => r.ProjectId == issuePitProject.Id))
        {
            db.GitRepositories.Add(new GitRepository { Id = Guid.NewGuid(), ProjectId = issuePitProject.Id, RemoteUrl = issuePitProject.GitHubRepo!, DefaultBranch = "main", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        // --- Dummy CI/CD Test project ---
        // Minimal repo used to validate CI/CD runtime: fast green runs for development and E2E testing.
        var (dummyCiCdProject, _) = await db.Projects.AddIfNotExistsAsync(
            p => p.OrgId == org.Id && p.Slug == "dummy-cicd-test",
            new Project
            {
                Id = Guid.NewGuid(),
                OrgId = org.Id,
                Name = "Dummy CI/CD Test",
                Slug = "dummy-cicd-test",
                Description = "Minimal repo used to validate the CI/CD runtime — fast green runs for development and E2E testing.",
                GitHubRepo = "https://github.com/issuepit/dummy-cicd-action-test",
                CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        if (!await db.GitRepositories.AnyAsync(r => r.ProjectId == dummyCiCdProject.Id))
        {
            db.GitRepositories.Add(new GitRepository { Id = Guid.NewGuid(), ProjectId = dummyCiCdProject.Id, RemoteUrl = "https://github.com/issuepit/dummy-cicd-action-test", DefaultBranch = "main", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var (dummyBoard, _) = await db.KanbanBoards.AddIfNotExistsAsync(b => b.ProjectId == dummyCiCdProject.Id && b.Name == "Main Board", new KanbanBoard { Id = Guid.NewGuid(), ProjectId = dummyCiCdProject.Id, Name = "Main Board", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == dummyBoard.Id && c.IssueStatus == IssueStatus.Backlog,    new KanbanColumn { Id = Guid.NewGuid(), BoardId = dummyBoard.Id, Name = "Backlog",     Position = 0, IssueStatus = IssueStatus.Backlog });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == dummyBoard.Id && c.IssueStatus == IssueStatus.Todo,       new KanbanColumn { Id = Guid.NewGuid(), BoardId = dummyBoard.Id, Name = "To Do",       Position = 1, IssueStatus = IssueStatus.Todo });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == dummyBoard.Id && c.IssueStatus == IssueStatus.InProgress, new KanbanColumn { Id = Guid.NewGuid(), BoardId = dummyBoard.Id, Name = "In Progress", Position = 2, IssueStatus = IssueStatus.InProgress });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == dummyBoard.Id && c.IssueStatus == IssueStatus.Done,       new KanbanColumn { Id = Guid.NewGuid(), BoardId = dummyBoard.Id, Name = "Done",        Position = 4, IssueStatus = IssueStatus.Done });
        await db.SaveChangesAsync();

        var (ipLabelBug, _)         = await db.Labels.AddIfNotExistsAsync(l => l.ProjectId == issuePitProject.Id && l.Name == "bug",         new Label { Id = Guid.NewGuid(), ProjectId = issuePitProject.Id, Name = "bug",         Color = "#e11d48" });
        var (ipLabelFeature, _)     = await db.Labels.AddIfNotExistsAsync(l => l.ProjectId == issuePitProject.Id && l.Name == "feature",     new Label { Id = Guid.NewGuid(), ProjectId = issuePitProject.Id, Name = "feature",     Color = "#2563eb" });
        var (ipLabelEnhancement, _) = await db.Labels.AddIfNotExistsAsync(l => l.ProjectId == issuePitProject.Id && l.Name == "enhancement", new Label { Id = Guid.NewGuid(), ProjectId = issuePitProject.Id, Name = "enhancement", Color = "#0891b2" });
        var (ipLabelDocs, _)        = await db.Labels.AddIfNotExistsAsync(l => l.ProjectId == issuePitProject.Id && l.Name == "docs",        new Label { Id = Guid.NewGuid(), ProjectId = issuePitProject.Id, Name = "docs",        Color = "#7c3aed" });
        await db.SaveChangesAsync();

        var (ipBoard, _) = await db.KanbanBoards.AddIfNotExistsAsync(b => b.ProjectId == issuePitProject.Id && b.Name == "Main Board", new KanbanBoard { Id = Guid.NewGuid(), ProjectId = issuePitProject.Id, Name = "Main Board", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == ipBoard.Id && c.IssueStatus == IssueStatus.Backlog,    new KanbanColumn { Id = Guid.NewGuid(), BoardId = ipBoard.Id, Name = "Backlog",     Position = 0, IssueStatus = IssueStatus.Backlog });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == ipBoard.Id && c.IssueStatus == IssueStatus.Todo,       new KanbanColumn { Id = Guid.NewGuid(), BoardId = ipBoard.Id, Name = "To Do",       Position = 1, IssueStatus = IssueStatus.Todo });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == ipBoard.Id && c.IssueStatus == IssueStatus.InProgress, new KanbanColumn { Id = Guid.NewGuid(), BoardId = ipBoard.Id, Name = "In Progress", Position = 2, IssueStatus = IssueStatus.InProgress });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == ipBoard.Id && c.IssueStatus == IssueStatus.InReview,   new KanbanColumn { Id = Guid.NewGuid(), BoardId = ipBoard.Id, Name = "In Review",   Position = 3, IssueStatus = IssueStatus.InReview });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == ipBoard.Id && c.IssueStatus == IssueStatus.Done,       new KanbanColumn { Id = Guid.NewGuid(), BoardId = ipBoard.Id, Name = "Done",        Position = 4, IssueStatus = IssueStatus.Done });
        await db.SaveChangesAsync();

        // --- IssuePit milestones ---
        var (ipMilestoneBeta, _) = await db.Milestones.AddIfNotExistsAsync(m => m.ProjectId == issuePitProject.Id && m.Title == "v0.1 Private Beta",
            new Milestone { Id = Guid.NewGuid(), ProjectId = issuePitProject.Id, Title = "v0.1 Private Beta", Description = "Core issue tracking, kanban, and agent sessions working end-to-end", StartDate = DateTime.UtcNow.AddDays(-37), DueDate = DateTime.UtcNow.AddDays(-7), Status = MilestoneStatus.Closed, CreatedAt = DateTime.UtcNow.AddDays(-30) });
        var (ipMilestoneCiCd, _) = await db.Milestones.AddIfNotExistsAsync(m => m.ProjectId == issuePitProject.Id && m.Title == "v0.2 CI/CD & Code Review",
            new Milestone { Id = Guid.NewGuid(), ProjectId = issuePitProject.Id, Title = "v0.2 CI/CD & Code Review", Description = "CI/CD run tracking, code review comments, and GitHub integration", StartDate = DateTime.UtcNow.AddDays(-14), DueDate = DateTime.UtcNow.AddDays(21), CreatedAt = DateTime.UtcNow.AddDays(-14) });
        await db.SaveChangesAsync();

        // IssuePit issues — dates spread across 14 days
        var (ipIssue1, ipIssue1IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == issuePitProject.Id && i.Number == 1, CreateDemoIssue(issuePitProject.Id, 1, "feat: issue editor improvements", "Make the issue page use more screen real estate, add comments, label manager, type changing, assign to user/agent, create sub-issues and tasks.", IssueStatus.InProgress, IssuePriority.High, IssueType.Feature, createdDaysAgo: 13, updatedDaysAgo: 3));
        var (ipIssue2, ipIssue2IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == issuePitProject.Id && i.Number == 2, CreateDemoIssue(issuePitProject.Id, 2, "feat: kanban board drag-and-drop", "Allow dragging issue cards between kanban columns to change their status.", IssueStatus.Todo, IssuePriority.Medium, IssueType.Feature, createdDaysAgo: 11, updatedDaysAgo: 11));
        var (ipIssue3, ipIssue3IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == issuePitProject.Id && i.Number == 3, CreateDemoIssue(issuePitProject.Id, 3, "fix: agent session logs not streaming", "Agent session logs are not streaming in real time via SignalR — only show after completion.", IssueStatus.InProgress, IssuePriority.Urgent, IssueType.Bug, createdDaysAgo: 4, updatedDaysAgo: 1));
        var (ipIssue4, ipIssue4IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == issuePitProject.Id && i.Number == 4, CreateDemoIssue(issuePitProject.Id, 4, "feat: GitHub webhook integration", "Sync issues and PRs from GitHub repositories via webhooks.", IssueStatus.Backlog, IssuePriority.Low, IssueType.Feature, createdDaysAgo: 14, updatedDaysAgo: 14));
        var (ipIssue5, ipIssue5IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == issuePitProject.Id && i.Number == 5, CreateDemoIssue(issuePitProject.Id, 5, "chore: extend seed data", "Add richer seed data including the issuepit/issuepit project itself.", IssueStatus.Done, IssuePriority.NoPriority, IssueType.Task, createdDaysAgo: 7, updatedDaysAgo: 5));
        var (ipIssue6, ipIssue6IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == issuePitProject.Id && i.Number == 6, CreateDemoIssue(issuePitProject.Id, 6, "fix: data seeding and priority colors", "Seed most issues with medium/low priority, change priority color scheme, add milestones and CI/CD log examples.", IssueStatus.InProgress, IssuePriority.Medium, IssueType.Bug, createdDaysAgo: 2, updatedDaysAgo: 1));
        var (ipIssue7, ipIssue7IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == issuePitProject.Id && i.Number == 7, CreateDemoIssue(issuePitProject.Id, 7, "feat: CI/CD log color rules", "Allow users to define regex patterns that colorize specific log lines (e.g. errors in red, warnings in yellow).", IssueStatus.Backlog, IssuePriority.Low, IssueType.Feature, createdDaysAgo: 6, updatedDaysAgo: 6));
        var (ipIssue8, ipIssue8IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == issuePitProject.Id && i.Number == 8, CreateDemoIssue(issuePitProject.Id, 8, "chore: improve E2E test coverage", "Add E2E tests for kanban board, issue creation, and agent session flows.", IssueStatus.Todo, IssuePriority.Low, IssueType.Task, createdDaysAgo: 5, updatedDaysAgo: 5));
        await db.SaveChangesAsync();

        // Attach labels to newly created issues
        if (ipIssue1IsNew) { ipIssue1.Labels.Add(ipLabelFeature); ipIssue1.Labels.Add(ipLabelEnhancement); }
        if (ipIssue3IsNew) ipIssue3.Labels.Add(ipLabelBug);
        if (ipIssue4IsNew) ipIssue4.Labels.Add(ipLabelFeature);
        if (ipIssue5IsNew) ipIssue5.Labels.Add(ipLabelDocs);
        if (ipIssue6IsNew) ipIssue6.Labels.Add(ipLabelBug);
        if (ipIssue7IsNew) ipIssue7.Labels.Add(ipLabelFeature);
        if (ipIssue8IsNew) ipIssue8.Labels.Add(ipLabelDocs);
        await db.SaveChangesAsync();

        // Assign IssuePit issues to milestones (idempotent)
        if (ipIssue1.MilestoneId is null) ipIssue1.MilestoneId = ipMilestoneBeta.Id;
        if (ipIssue2.MilestoneId is null) ipIssue2.MilestoneId = ipMilestoneBeta.Id;
        if (ipIssue3.MilestoneId is null) ipIssue3.MilestoneId = ipMilestoneCiCd.Id;
        if (ipIssue4.MilestoneId is null) ipIssue4.MilestoneId = ipMilestoneCiCd.Id;
        if (ipIssue5.MilestoneId is null) ipIssue5.MilestoneId = ipMilestoneBeta.Id;
        if (ipIssue6.MilestoneId is null) ipIssue6.MilestoneId = ipMilestoneCiCd.Id;
        if (ipIssue7.MilestoneId is null) ipIssue7.MilestoneId = ipMilestoneCiCd.Id;
        if (ipIssue8.MilestoneId is null) ipIssue8.MilestoneId = ipMilestoneCiCd.Id;
        await db.SaveChangesAsync();

        // Add tasks to ipIssue1 (only if newly created)
        if (ipIssue1IsNew)
        {
            db.IssueTasks.AddRange(
                new IssueTask { Id = Guid.NewGuid(), IssueId = ipIssue1.Id, Title = "Redesign issue detail page layout", Body = "Use full-width layout and improved sidebar", Status = IssueStatus.Done, CreatedAt = DateTime.UtcNow.AddDays(-2) },
                new IssueTask { Id = Guid.NewGuid(), IssueId = ipIssue1.Id, Title = "Add comment functionality", Body = "Allow users to add and delete comments on issues", Status = IssueStatus.InProgress, CreatedAt = DateTime.UtcNow.AddDays(-2) },
                new IssueTask { Id = Guid.NewGuid(), IssueId = ipIssue1.Id, Title = "Label manager in sidebar", Body = "Add/remove labels from an issue", Status = IssueStatus.Todo, CreatedAt = DateTime.UtcNow.AddDays(-1) },
                new IssueTask { Id = Guid.NewGuid(), IssueId = ipIssue1.Id, Title = "Make issue type editable", Body = "Allow changing the type from the sidebar dropdown", Status = IssueStatus.Todo, CreatedAt = DateTime.UtcNow.AddDays(-1) }
            );
            await db.SaveChangesAsync();
        }

        // --- Common Agenda project ---
        var (agendaProject, _) = await db.Projects.AddIfNotExistsAsync(
            p => p.OrgId == org.Id && p.Slug == "common-agenda",
            new Project
            {
                Id = Guid.NewGuid(),
                OrgId = org.Id,
                Name = "Common Agenda",
                Slug = "common-agenda",
                Description = "Org-wide goal tracker — cross-cutting initiatives that span all projects (AI skills, security tooling, library upgrades, CI/CD patterns)",
                IsAgenda = true,
                CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var (agendaLabelAi, _)       = await db.Labels.AddIfNotExistsAsync(l => l.ProjectId == agendaProject.Id && l.Name == "ai",           new Label { Id = Guid.NewGuid(), ProjectId = agendaProject.Id, Name = "ai",           Color = "#7c3aed" });
        var (agendaLabelSecurity, _) = await db.Labels.AddIfNotExistsAsync(l => l.ProjectId == agendaProject.Id && l.Name == "security",     new Label { Id = Guid.NewGuid(), ProjectId = agendaProject.Id, Name = "security",     Color = "#e11d48" });
        var (agendaLabelCiCd, _)     = await db.Labels.AddIfNotExistsAsync(l => l.ProjectId == agendaProject.Id && l.Name == "ci-cd",        new Label { Id = Guid.NewGuid(), ProjectId = agendaProject.Id, Name = "ci-cd",        Color = "#0891b2" });
        var (agendaLabelDeps, _)     = await db.Labels.AddIfNotExistsAsync(l => l.ProjectId == agendaProject.Id && l.Name == "dependencies", new Label { Id = Guid.NewGuid(), ProjectId = agendaProject.Id, Name = "dependencies", Color = "#d97706" });
        await db.SaveChangesAsync();

        var (agendaBoard, _) = await db.KanbanBoards.AddIfNotExistsAsync(b => b.ProjectId == agendaProject.Id && b.Name == "Agenda Board", new KanbanBoard { Id = Guid.NewGuid(), ProjectId = agendaProject.Id, Name = "Agenda Board", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == agendaBoard.Id && c.IssueStatus == IssueStatus.Backlog,    new KanbanColumn { Id = Guid.NewGuid(), BoardId = agendaBoard.Id, Name = "Backlog",     Position = 0, IssueStatus = IssueStatus.Backlog });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == agendaBoard.Id && c.IssueStatus == IssueStatus.Todo,       new KanbanColumn { Id = Guid.NewGuid(), BoardId = agendaBoard.Id, Name = "To Do",       Position = 1, IssueStatus = IssueStatus.Todo });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == agendaBoard.Id && c.IssueStatus == IssueStatus.InProgress, new KanbanColumn { Id = Guid.NewGuid(), BoardId = agendaBoard.Id, Name = "In Progress", Position = 2, IssueStatus = IssueStatus.InProgress });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == agendaBoard.Id && c.IssueStatus == IssueStatus.InReview,   new KanbanColumn { Id = Guid.NewGuid(), BoardId = agendaBoard.Id, Name = "In Review",   Position = 3, IssueStatus = IssueStatus.InReview });
        await db.KanbanColumns.AddIfNotExistsAsync(c => c.BoardId == agendaBoard.Id && c.IssueStatus == IssueStatus.Done,       new KanbanColumn { Id = Guid.NewGuid(), BoardId = agendaBoard.Id, Name = "Done",        Position = 4, IssueStatus = IssueStatus.Done });
        await db.SaveChangesAsync();

        // Agenda issues — dates spread across 14 days, balanced priorities
        var (agendaIssue1, agendaIssue1IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == agendaProject.Id && i.Number == 1, CreateDemoIssue(agendaProject.Id, 1, "Add AI coding skills to all repos", "Configure opencode/copilot agent modes and system prompts for all projects in the org. Each repo should have a plan, code, and evaluate agent mode.", IssueStatus.InProgress, IssuePriority.High, IssueType.Feature, createdDaysAgo: 12, updatedDaysAgo: 4));
        var (agendaIssue2, agendaIssue2IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == agendaProject.Id && i.Number == 2, CreateDemoIssue(agendaProject.Id, 2, "Add SAST security scanner to all CI/CD pipelines", "Integrate a static application security testing (SAST) tool (e.g. Semgrep, CodeQL) into every project's CI/CD workflow.", IssueStatus.Todo, IssuePriority.Urgent, IssueType.Feature, createdDaysAgo: 9, updatedDaysAgo: 9));
        var (agendaIssue3, agendaIssue3IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == agendaProject.Id && i.Number == 3, CreateDemoIssue(agendaProject.Id, 3, "Migrate from deprecated logging library to OpenTelemetry", "The current logging library is end-of-life. Migrate all services to OpenTelemetry for unified observability.", IssueStatus.Backlog, IssuePriority.Medium, IssueType.Task, createdDaysAgo: 14, updatedDaysAgo: 14));
        var (agendaIssue4, agendaIssue4IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == agendaProject.Id && i.Number == 4, CreateDemoIssue(agendaProject.Id, 4, "Standardize Dockerfile base images across org", "All projects should use pinned, minimal base images (e.g. distroless). Document the approved image list.", IssueStatus.Todo, IssuePriority.Low, IssueType.Task, createdDaysAgo: 6, updatedDaysAgo: 6));
        var (agendaIssue5, agendaIssue5IsNew) = await db.Issues.AddIfNotExistsAsync(i => i.ProjectId == agendaProject.Id && i.Number == 5, CreateDemoIssue(agendaProject.Id, 5, "Enforce branch protection rules on all repos", "Main branches on all GitHub repositories should require PR reviews and passing CI before merge.", IssueStatus.Done, IssuePriority.NoPriority, IssueType.Task, createdDaysAgo: 14, updatedDaysAgo: 8));
        await db.SaveChangesAsync();

        if (agendaIssue1IsNew) agendaIssue1.Labels.Add(agendaLabelAi);
        if (agendaIssue2IsNew) { agendaIssue2.Labels.Add(agendaLabelSecurity); agendaIssue2.Labels.Add(agendaLabelCiCd); }
        if (agendaIssue3IsNew) agendaIssue3.Labels.Add(agendaLabelDeps);
        if (agendaIssue4IsNew) agendaIssue4.Labels.Add(agendaLabelCiCd);
        if (agendaIssue5IsNew) agendaIssue5.Labels.Add(agendaLabelSecurity);
        await db.SaveChangesAsync();

        // Cross-project links: agenda issue 1 → linked to IssuePit and frontend; agenda issue 2 → linked to backend and IssuePit issues
        await db.IssueLinks.AddIfNotExistsAsync(l => l.IssueId == agendaIssue1.Id && l.TargetIssueId == ipIssue1.Id, new IssueLink { Id = Guid.NewGuid(), IssueId = agendaIssue1.Id, TargetIssueId = ipIssue1.Id, LinkType = IssueLinkType.LinkedTo, CreatedAt = DateTime.UtcNow });
        await db.IssueLinks.AddIfNotExistsAsync(l => l.IssueId == agendaIssue1.Id && l.TargetIssueId == feIssue1.Id, new IssueLink { Id = Guid.NewGuid(), IssueId = agendaIssue1.Id, TargetIssueId = feIssue1.Id, LinkType = IssueLinkType.LinkedTo, CreatedAt = DateTime.UtcNow });
        await db.IssueLinks.AddIfNotExistsAsync(l => l.IssueId == agendaIssue2.Id && l.TargetIssueId == beIssue1.Id, new IssueLink { Id = Guid.NewGuid(), IssueId = agendaIssue2.Id, TargetIssueId = beIssue1.Id, LinkType = IssueLinkType.LinkedTo, CreatedAt = DateTime.UtcNow });
        await db.IssueLinks.AddIfNotExistsAsync(l => l.IssueId == agendaIssue2.Id && l.TargetIssueId == ipIssue3.Id, new IssueLink { Id = Guid.NewGuid(), IssueId = agendaIssue2.Id, TargetIssueId = ipIssue3.Id, LinkType = IssueLinkType.LinkedTo, CreatedAt = DateTime.UtcNow });
        await db.IssueLinks.AddIfNotExistsAsync(l => l.IssueId == agendaIssue3.Id && l.TargetIssueId == beIssue6.Id, new IssueLink { Id = Guid.NewGuid(), IssueId = agendaIssue3.Id, TargetIssueId = beIssue6.Id, LinkType = IssueLinkType.LinkedTo, CreatedAt = DateTime.UtcNow });
        await db.IssueLinks.AddIfNotExistsAsync(l => l.IssueId == agendaIssue4.Id && l.TargetIssueId == feIssue3.Id, new IssueLink { Id = Guid.NewGuid(), IssueId = agendaIssue4.Id, TargetIssueId = feIssue3.Id, LinkType = IssueLinkType.LinkedTo, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        // --- Issue Assignees ---
        if (adminUser is not null)
        {
            // Admin assigned to key IssuePit issues and one from each project
            await db.IssueAssignees.AddIfNotExistsAsync(a => a.IssueId == ipIssue1.Id     && a.UserId == adminUser.Id, new IssueAssignee { Id = Guid.NewGuid(), IssueId = ipIssue1.Id,     UserId = adminUser.Id });
            await db.IssueAssignees.AddIfNotExistsAsync(a => a.IssueId == ipIssue3.Id     && a.UserId == adminUser.Id, new IssueAssignee { Id = Guid.NewGuid(), IssueId = ipIssue3.Id,     UserId = adminUser.Id });
            await db.IssueAssignees.AddIfNotExistsAsync(a => a.IssueId == feIssue2.Id     && a.UserId == adminUser.Id, new IssueAssignee { Id = Guid.NewGuid(), IssueId = feIssue2.Id,     UserId = adminUser.Id }); // FE keyboard shortcuts
            await db.IssueAssignees.AddIfNotExistsAsync(a => a.IssueId == agendaIssue1.Id && a.UserId == adminUser.Id, new IssueAssignee { Id = Guid.NewGuid(), IssueId = agendaIssue1.Id, UserId = adminUser.Id });
        }
        if (aliceUser is not null)
        {
            // Alice assigned to frontend and agenda issues
            await db.IssueAssignees.AddIfNotExistsAsync(a => a.IssueId == feIssue1.Id     && a.UserId == aliceUser.Id, new IssueAssignee { Id = Guid.NewGuid(), IssueId = feIssue1.Id,     UserId = aliceUser.Id }); // dark mode flicker
            await db.IssueAssignees.AddIfNotExistsAsync(a => a.IssueId == feIssue5.Id     && a.UserId == aliceUser.Id, new IssueAssignee { Id = Guid.NewGuid(), IssueId = feIssue5.Id,     UserId = aliceUser.Id }); // rich text editor
            await db.IssueAssignees.AddIfNotExistsAsync(a => a.IssueId == ipIssue2.Id     && a.UserId == aliceUser.Id, new IssueAssignee { Id = Guid.NewGuid(), IssueId = ipIssue2.Id,     UserId = aliceUser.Id });
            await db.IssueAssignees.AddIfNotExistsAsync(a => a.IssueId == agendaIssue2.Id && a.UserId == aliceUser.Id, new IssueAssignee { Id = Guid.NewGuid(), IssueId = agendaIssue2.Id, UserId = aliceUser.Id });
        }
        if (bobUser is not null)
        {
            // Bob assigned to backend and remaining issues
            await db.IssueAssignees.AddIfNotExistsAsync(a => a.IssueId == beIssue1.Id     && a.UserId == bobUser.Id, new IssueAssignee { Id = Guid.NewGuid(), IssueId = beIssue1.Id,     UserId = bobUser.Id }); // BE rate limiting
            await db.IssueAssignees.AddIfNotExistsAsync(a => a.IssueId == beIssue2.Id     && a.UserId == bobUser.Id, new IssueAssignee { Id = Guid.NewGuid(), IssueId = beIssue2.Id,     UserId = bobUser.Id }); // slow query
            await db.IssueAssignees.AddIfNotExistsAsync(a => a.IssueId == ipIssue4.Id     && a.UserId == bobUser.Id, new IssueAssignee { Id = Guid.NewGuid(), IssueId = ipIssue4.Id,     UserId = bobUser.Id });
            await db.IssueAssignees.AddIfNotExistsAsync(a => a.IssueId == agendaIssue3.Id && a.UserId == bobUser.Id, new IssueAssignee { Id = Guid.NewGuid(), IssueId = agendaIssue3.Id, UserId = bobUser.Id });
        }
        await db.SaveChangesAsync();

        // --- Issue Comments (only for newly seeded issues) ---
        var comments = new List<IssueComment>();

        if (aliceUser is not null)
        {
            if (feIssue1IsNew)     comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = feIssue1.Id,     UserId = aliceUser.Id, Body = "Reproduced on Safari 17. The flash happens because the theme class is applied after hydration. We should apply it server-side via a cookie check.", CreatedAt = DateTime.UtcNow.AddDays(-12) });
            if (beIssue2IsNew)     comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = beIssue2.Id,     UserId = aliceUser.Id, Body = "The N+1 is in `IssueListQueryHandler`. We need `.Include(i => i.Labels)` and a composite index on `(project_id, status)`.", CreatedAt = DateTime.UtcNow.AddDays(-7) });
            if (ipIssue1IsNew)     comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = ipIssue1.Id,     UserId = aliceUser.Id, Body = "I've started on the redesign task. Going with a 3-column layout: nav / issue content / metadata sidebar. Should work well on 1280px+.", CreatedAt = DateTime.UtcNow.AddDays(-2) });
            if (agendaIssue1IsNew) comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = agendaIssue1.Id, UserId = aliceUser.Id, Body = "We already have opencode set up for IssuePit itself. I can use that config as a template for the other repos.", CreatedAt = DateTime.UtcNow.AddDays(-3) });
        }
        if (bobUser is not null)
        {
            if (feIssue1IsNew)     comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = feIssue1.Id,     UserId = bobUser.Id, Body = "Agree with Alice. Alternatively we could use a `<script>` block in `<head>` that reads localStorage and applies the class before React/Vue mounts.", CreatedAt = DateTime.UtcNow.AddDays(-11) });
            if (beIssue1IsNew)     comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = beIssue1.Id,     UserId = bobUser.Id, Body = "I'll implement this using a token bucket algorithm. The rate limits will be configurable via environment variables.", CreatedAt = DateTime.UtcNow.AddDays(-11) });
            if (beIssue2IsNew)     comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = beIssue2.Id,     UserId = bobUser.Id, Body = "Fixed the N+1. Added `.Include()` calls and a migration with the composite index. Tests pass locally. Ready for review.", CreatedAt = DateTime.UtcNow.AddDays(-5) });
            if (ipIssue3IsNew)     comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = ipIssue3.Id,     UserId = bobUser.Id, Body = "The root cause is that the SignalR group name was using the session ID before it was persisted. Moving `AddToGroupAsync` after `SaveChangesAsync` should fix it.", CreatedAt = DateTime.UtcNow.AddDays(-1) });
        }
        if (adminUser is not null)
        {
            if (ipIssue1IsNew)     comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = ipIssue1.Id,     UserId = adminUser.Id, Body = "The comment section should support Markdown rendering. Check how the issue body renderer works and reuse it.", CreatedAt = DateTime.UtcNow.AddDays(-1) });
            if (ipIssue3IsNew)     comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = ipIssue3.Id,     UserId = adminUser.Id, Body = "This is blocking our demo — marking as urgent. @bob please prioritize.", CreatedAt = DateTime.UtcNow.AddDays(-2) });
            if (agendaIssue2IsNew) comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = agendaIssue2.Id, UserId = adminUser.Id, Body = "CodeQL is already set up for issuepit/issuepit. We should copy the workflow to the other repos as a starting point.", CreatedAt = DateTime.UtcNow.AddDays(-8) });
            if (beIssue1IsNew)     comments.Add(new IssueComment { Id = Guid.NewGuid(), IssueId = beIssue1.Id,     UserId = adminUser.Id, Body = "Make sure the rate limit headers (`X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`) are included in the response.", CreatedAt = DateTime.UtcNow.AddDays(-10) });
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
        await db.CodeReviewComments.AddIfNotExistsAsync(
            c => c.IssueId == beIssue2.Id && c.FilePath == "src/IssuePit.Api/QueryHandlers/IssueListQueryHandler.cs" && c.StartLine == 42,
            new CodeReviewComment
            {
                Id = Guid.NewGuid(),
                IssueId = beIssue2.Id, // slow query fix
                FilePath = "src/IssuePit.Api/QueryHandlers/IssueListQueryHandler.cs",
                StartLine = 42, EndLine = 44,
                Sha = demoSha1,
                Snippet = "    var issues = await db.Issues\n        .Where(i => i.ProjectId == query.ProjectId)\n        .ToListAsync();",
                ContextBefore = "public async Task<List<IssueDto>> Handle(IssueListQuery query, CancellationToken ct)\n{",
                ContextAfter = "    return issues.Select(IssueDto.From).ToList();\n}",
                Body = "**N+1 detected** — labels are loaded lazily after this query. Add `.Include(i => i.Labels)` here to load them in a single query.",
                CreatedAt = DateTime.UtcNow.AddDays(-6),
            });
        await db.CodeReviewComments.AddIfNotExistsAsync(
            c => c.IssueId == beIssue2.Id && c.FilePath == "src/IssuePit.Core/Migrations/20260210_AddLabelIndex.cs" && c.StartLine == 12,
            new CodeReviewComment
            {
                Id = Guid.NewGuid(),
                IssueId = beIssue2.Id,
                FilePath = "src/IssuePit.Core/Migrations/20260210_AddLabelIndex.cs",
                StartLine = 12, EndLine = 14,
                Sha = demoSha1,
                Snippet = "migrationBuilder.CreateIndex(\n    name: \"ix_issues_project_id\",\n    table: \"issues\",",
                ContextBefore = "protected override void Up(MigrationBuilder migrationBuilder)\n{",
                ContextAfter = "    columns: new[] { \"project_id\" });",
                Body = "Consider making this a composite index `(project_id, status)` since the issue list is almost always filtered by both.",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
            });
        await db.CodeReviewComments.AddIfNotExistsAsync(
            c => c.IssueId == ipIssue3.Id && c.FilePath == "src/IssuePit.Api/Hubs/AgentSessionHub.cs" && c.StartLine == 28,
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
            });
        await db.SaveChangesAsync();

        // --- Project Metric Snapshots (14 days of daily history for IssuePit and Frontend projects) ---
        // Simulates a project that starts with 4 open issues and gradually moves work through InProgress to Done.
        const int MetricDays = 14;
        var snapshotProjects = new[] { issuePitProject, frontendProject };
        var metricSnapshots = new List<ProjectMetricSnapshot>();
        foreach (var proj in snapshotProjects)
        {
            // Only add snapshots if none exist yet for this project
            if (!await db.ProjectMetricSnapshots.AnyAsync(s => s.ProjectId == proj.Id))
            {
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
        }
        if (metricSnapshots.Count > 0)
        {
            db.ProjectMetricSnapshots.AddRange(metricSnapshots);
            await db.SaveChangesAsync();
        }

        // --- Demo CI/CD Run with sample log lines (color regex example) ---
        // Shows various log patterns: info, warnings, errors, steps — useful for testing log colorization features
        const string cicdSha = "deadbeef1234567890abcdef1234567890abcdef";
        var (demoCicdRun, cicdRunIsNew) = await db.CiCdRuns.AddIfNotExistsAsync(
            r => r.ProjectId == issuePitProject.Id && r.CommitSha == cicdSha,
            new CiCdRun
            {
                Id = Guid.NewGuid(),
                ProjectId = issuePitProject.Id,
                CommitSha = cicdSha,
                Branch = "main",
                Workflow = "ci.yml",
                Status = CiCdRunStatus.Succeeded,
                StartedAt = DateTime.UtcNow.AddHours(-2),
                EndedAt = DateTime.UtcNow.AddHours(-1).AddMinutes(-47),
            });
        await db.SaveChangesAsync();

        if (cicdRunIsNew)
        {
            var logBase = demoCicdRun.StartedAt;
            db.CiCdRunLogs.AddRange(
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "[INFO]  Starting CI pipeline for branch 'main'", Timestamp = logBase.AddSeconds(1) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "[INFO]  Checking out commit deadbeef...", Timestamp = logBase.AddSeconds(2) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "✔ Checkout complete", Timestamp = logBase.AddSeconds(4) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "[INFO]  Restoring .NET packages...", Timestamp = logBase.AddSeconds(5) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "✔ Package restore succeeded (12.3s)", Timestamp = logBase.AddSeconds(18) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "[INFO]  Building solution...", Timestamp = logBase.AddSeconds(19) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stderr, Line = "[WARNING] CS0618: 'OldMethod' is obsolete: 'Use NewMethod instead'", Timestamp = logBase.AddSeconds(35) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "✔ Build succeeded — 0 error(s), 1 warning(s) (28.4s)", Timestamp = logBase.AddSeconds(48) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "[INFO]  Running unit tests...", Timestamp = logBase.AddSeconds(49) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "✔ IssuePit.Tests.Unit  — 84 passed, 0 failed (6.1s)", Timestamp = logBase.AddSeconds(56) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "[INFO]  Running integration tests...", Timestamp = logBase.AddSeconds(57) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "✔ IssuePit.Tests.Integration — 31 passed, 0 failed (22.7s)", Timestamp = logBase.AddSeconds(80) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "[INFO]  Installing frontend dependencies...", Timestamp = logBase.AddSeconds(81) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stderr, Line = "[WARNING] 3 moderate severity vulnerabilities found in dependencies", Timestamp = logBase.AddSeconds(95) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "✔ npm install complete (14.2s)", Timestamp = logBase.AddSeconds(96) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "[INFO]  Building frontend...", Timestamp = logBase.AddSeconds(97) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "✔ Nuxt build succeeded (41.8s)", Timestamp = logBase.AddSeconds(140) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "[INFO]  Running linter...", Timestamp = logBase.AddSeconds(141) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "✔ ESLint — no issues found (3.2s)", Timestamp = logBase.AddSeconds(145) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "[INFO]  Publishing Docker image...", Timestamp = logBase.AddSeconds(146) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "✔ Image pushed: ghcr.io/issuepit/issuepit:deadbeef (8.3s)", Timestamp = logBase.AddSeconds(155) },
                new CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = demoCicdRun.Id, Stream = LogStream.Stdout, Line = "✔ Pipeline completed successfully in 2m 35s", Timestamp = logBase.AddSeconds(156) }
            );
            await db.SaveChangesAsync();
        }

        logger.LogInformation("Demo data seeded: org 'Acme Corp', 5 projects (Frontend, Backend API, IssuePit, Dummy CI/CD Test, Common Agenda), 28 issues, 4 agents, 2 MCP servers, milestones, assignees, comments, code review comments, CI/CD run, metric snapshots.");
    }

    private async Task SeedEvilCorpAsync(Guid tenantId)
    {
        logger.LogInformation("Seeding EvilCorp organization with thematic teams...");

        var (evilCorpOrg, _) = await db.Organizations.AddIfNotExistsAsync(
            o => o.TenantId == tenantId && o.Slug == "evilcorp",
            new Organization { Id = Guid.NewGuid(), TenantId = tenantId, Name = "EvilCorp", Slug = "evilcorp", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        // Resolve users by username
        User? GetUser(string username) =>
            db.Users.Local.FirstOrDefault(u => u.Username == username && u.TenantId == tenantId)
            ?? db.Users.FirstOrDefault(u => u.Username == username && u.TenantId == tenantId);

        // --- Teams ---
        var (teamHonest, _)    = await db.Teams.AddIfNotExistsAsync(t => t.OrgId == evilCorpOrg.Id && t.Slug == "honest",    new Team { Id = Guid.NewGuid(), OrgId = evilCorpOrg.Id, Name = "Honest Participants", Slug = "honest",    Description = "Generic participants used in cryptographic protocol examples: the senders, receivers, and trusted parties.", CreatedAt = DateTime.UtcNow });
        var (teamAttackers, _) = await db.Teams.AddIfNotExistsAsync(t => t.OrgId == evilCorpOrg.Id && t.Slug == "attackers", new Team { Id = Guid.NewGuid(), OrgId = evilCorpOrg.Id, Name = "Attackers",            Slug = "attackers", Description = "Adversarial actors — eavesdroppers, MITM attackers, intruders, and Sybil identities.", CreatedAt = DateTime.UtcNow });
        var (teamTheorists, _) = await db.Teams.AddIfNotExistsAsync(t => t.OrgId == evilCorpOrg.Id && t.Slug == "theorists", new Team { Id = Guid.NewGuid(), OrgId = evilCorpOrg.Id, Name = "Theorists",            Slug = "theorists", Description = "Participants from formal proof systems: Arthur/Merlin complexity classes and zero-knowledge proof roles.", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        // Add org members and team memberships idempotently.
        // A HashSet prevents calling AddIfNotExistsAsync twice for the same user's OrgMember
        // within the same SaveChanges batch (some users appear in multiple teams; AddIfNotExistsAsync
        // queries the DB, not the EF change tracker, so a pending-but-unsaved entry would not be
        // found, causing a duplicate-key violation on SaveChangesAsync).
        var addedOrgMemberUserIds = new HashSet<Guid>();
        async Task AddMemberAsync(Team team, string username)
        {
            var user = GetUser(username);
            if (user is null) return;
            if (addedOrgMemberUserIds.Add(user.Id))
                await db.OrganizationMembers.AddIfNotExistsAsync(m => m.OrgId == evilCorpOrg.Id && m.UserId == user.Id, new OrganizationMember { OrgId = evilCorpOrg.Id, UserId = user.Id });
            await db.TeamMembers.AddIfNotExistsAsync(m => m.TeamId == team.Id && m.UserId == user.Id, new TeamMember { TeamId = team.Id, UserId = user.Id });
        }

        var honestUsernames = new[] { "alice", "bob", "carol", "caesar", "dave", "erin", "frank", "grace", "heidi", "ivan", "judy", "joe", "niaj", "olivia", "peggy", "rupert", "sybil", "trent", "victor", "walter" };
        var attackerUsernames = new[] { "eve", "mallory", "malice", "trudy", "oscar", "sybil", "hackerman" };
        var theoristUsernames = new[] { "arthur", "merlin", "zeke", "victor", "peggy" };

        foreach (var u in honestUsernames)   await AddMemberAsync(teamHonest,    u);
        foreach (var u in attackerUsernames) await AddMemberAsync(teamAttackers, u);
        foreach (var u in theoristUsernames) await AddMemberAsync(teamTheorists, u);

        await db.SaveChangesAsync();
        logger.LogInformation("EvilCorp seeded: 3 thematic teams (Honest Participants, Attackers, Theorists).");
    }

    private async Task SeedDemoTodosAsync(Guid tenantId)
    {
        logger.LogInformation("Seeding demo todos...");

        var now = DateTime.UtcNow;

        // --- Board: Work ---
        var (workBoard, _) = await db.TodoBoards.AddIfNotExistsAsync(
            b => b.TenantId == tenantId && b.Name == "Work",
            new TodoBoard { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Work", Description = "Work-related tasks and projects", CreatedAt = now });

        var (catBacklog, _)    = await db.TodoCategories.AddIfNotExistsAsync(c => c.BoardId == workBoard.Id && c.Name == "Backlog",      new TodoCategory { Id = Guid.NewGuid(), BoardId = workBoard.Id, Name = "Backlog",      Color = "#6b7280", Position = 0 });
        var (catInProgress, _) = await db.TodoCategories.AddIfNotExistsAsync(c => c.BoardId == workBoard.Id && c.Name == "In Progress",  new TodoCategory { Id = Guid.NewGuid(), BoardId = workBoard.Id, Name = "In Progress",  Color = "#3b82f6", Position = 1 });
        var (catReview, _)     = await db.TodoCategories.AddIfNotExistsAsync(c => c.BoardId == workBoard.Id && c.Name == "Review",       new TodoCategory { Id = Guid.NewGuid(), BoardId = workBoard.Id, Name = "Review",       Color = "#f59e0b", Position = 2 });
        var (catDone, _)       = await db.TodoCategories.AddIfNotExistsAsync(c => c.BoardId == workBoard.Id && c.Name == "Done",         new TodoCategory { Id = Guid.NewGuid(), BoardId = workBoard.Id, Name = "Done",         Color = "#10b981", Position = 3 });

        // --- Board: Personal ---
        var (personalBoard, _) = await db.TodoBoards.AddIfNotExistsAsync(
            b => b.TenantId == tenantId && b.Name == "Personal",
            new TodoBoard { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Personal", Description = "Personal tasks and errands", CreatedAt = now });

        var (catErrands, _)  = await db.TodoCategories.AddIfNotExistsAsync(c => c.BoardId == personalBoard.Id && c.Name == "Errands",  new TodoCategory { Id = Guid.NewGuid(), BoardId = personalBoard.Id, Name = "Errands",  Color = "#8b5cf6", Position = 0 });
        var (catHealth, _)   = await db.TodoCategories.AddIfNotExistsAsync(c => c.BoardId == personalBoard.Id && c.Name == "Health",   new TodoCategory { Id = Guid.NewGuid(), BoardId = personalBoard.Id, Name = "Health",   Color = "#ef4444", Position = 1 });
        var (catLearning, _) = await db.TodoCategories.AddIfNotExistsAsync(c => c.BoardId == personalBoard.Id && c.Name == "Learning", new TodoCategory { Id = Guid.NewGuid(), BoardId = personalBoard.Id, Name = "Learning", Color = "#06b6d4", Position = 2 });

        await db.SaveChangesAsync();

        // Helper to add a todo with board/category memberships if it does not already exist
        async Task AddTodoAsync(string title, string? body, TodoPriority priority,
            DateTime? dueDate, bool isCompleted, TodoBoard board, TodoCategory? category,
            TodoRecurringInterval recurring = TodoRecurringInterval.None)
        {
            var (todo, isNew) = await db.Todos.AddIfNotExistsAsync(
                t => t.TenantId == tenantId && t.Title == title,
                new Todo
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Title = title,
                    Body = body,
                    Priority = priority,
                    DueDate = dueDate,
                    RecurringInterval = recurring,
                    IsCompleted = isCompleted,
                    CreatedAt = now.AddDays(-new Random(42).Next(0, 14)),
                    UpdatedAt = now,
                });
            if (isNew)
            {
                db.TodoBoardMemberships.Add(new TodoBoardMembership { TodoId = todo.Id, BoardId = board.Id });
                if (category is not null)
                    db.TodoCategoryMemberships.Add(new TodoCategoryMembership { TodoId = todo.Id, CategoryId = category.Id });
                await db.SaveChangesAsync();
            }
        }

        // Work todos
        await AddTodoAsync("Design new API schema for v2", "Define request/response models for the upcoming API redesign.", TodoPriority.High, now.AddDays(3).Date.AddHours(10), false, workBoard, catBacklog);
        await AddTodoAsync("Write unit tests for auth module", "Cover JWT validation, refresh token rotation, and logout.", TodoPriority.High, now.AddDays(1).Date.AddHours(14), false, workBoard, catInProgress);
        await AddTodoAsync("Fix pagination bug on issues list", "Offset-based pagination breaks when items are deleted.", TodoPriority.Urgent, now.Date.AddHours(9).AddMinutes(30), false, workBoard, catInProgress);
        await AddTodoAsync("Code review: PR #42 agent streaming", "Review the SSE streaming implementation for agent logs.", TodoPriority.Medium, now.Date.AddHours(11), false, workBoard, catReview);
        await AddTodoAsync("Update OpenAPI docs", "Regenerate swagger docs after adding todo endpoints.", TodoPriority.Low, now.AddDays(5).Date.AddHours(16), false, workBoard, catBacklog);
        await AddTodoAsync("Deploy hotfix to staging", "Cherry-pick the auth fix and push to staging environment.", TodoPriority.Urgent, now.Date.AddHours(8), false, workBoard, catInProgress);
        await AddTodoAsync("Team standup preparation", "Prepare talking points for weekly team sync.", TodoPriority.Medium, now.AddDays(2).Date.AddHours(9), false, workBoard, catBacklog, TodoRecurringInterval.Weekly);
        await AddTodoAsync("Refactor database query layer", "Replace raw SQL with proper EF Core LINQ queries.", TodoPriority.Medium, now.AddDays(7).Date.AddHours(15), false, workBoard, catBacklog);
        await AddTodoAsync("Add dark mode support", "Implement dark/light theme toggle in the frontend.", TodoPriority.Low, now.AddDays(10).Date.AddHours(14), false, workBoard, catBacklog);
        await AddTodoAsync("Release v1.2.0 changelog", "Write and publish the changelog for the v1.2.0 release.", TodoPriority.Medium, now.AddDays(-1).Date.AddHours(17), true, workBoard, catDone);
        await AddTodoAsync("Set up CI/CD pipeline", "Configure GitHub Actions for automated builds and tests.", TodoPriority.High, now.AddDays(-3).Date.AddHours(12), true, workBoard, catDone);

        // Personal todos
        await AddTodoAsync("Grocery shopping", "Milk, eggs, bread, vegetables, and fruit.", TodoPriority.Medium, now.Date.AddHours(18), false, personalBoard, catErrands);
        await AddTodoAsync("Morning run", "5km at the park before work.", TodoPriority.Medium, now.AddDays(1).Date.AddHours(7), false, personalBoard, catHealth, TodoRecurringInterval.Daily);
        await AddTodoAsync("Read 'Clean Code'", "Finish chapters 8-10 of Clean Code by Robert C. Martin.", TodoPriority.Low, now.AddDays(4).Date.AddHours(21), false, personalBoard, catLearning);
        await AddTodoAsync("Doctor appointment", "Annual check-up at 2:30 PM.", TodoPriority.High, now.AddDays(2).Date.AddHours(14).AddMinutes(30), false, personalBoard, catHealth);
        await AddTodoAsync("Buy birthday present for Alice", "Get something thoughtful for her birthday next week.", TodoPriority.Medium, now.AddDays(6).Date.AddHours(12), false, personalBoard, catErrands);
        await AddTodoAsync("Complete Vue 3 course", "Finish the advanced composition API sections.", TodoPriority.Medium, now.AddDays(14).Date.AddHours(20), false, personalBoard, catLearning);
        await AddTodoAsync("Oil change for car", "Take the car to the service center.", TodoPriority.Low, now.AddDays(8).Date.AddHours(11), false, personalBoard, catErrands);
        await AddTodoAsync("Meal prep Sunday", "Prepare lunches for the week.", TodoPriority.Medium, now.AddDays(-2).Date.AddHours(15), true, personalBoard, catHealth, TodoRecurringInterval.Weekly);

        logger.LogInformation("Demo todos seeded: 2 boards (Work, Personal) with 7 categories and 19 todos.");
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
