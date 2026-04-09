using IssuePit.Notes.Core.Data;
using IssuePit.Notes.Core.Entities;
using IssuePit.Notes.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IssuePit.Notes.Migrator;

public class NotesDemoDataSeeder(NotesDbContext notesDb, ILogger<NotesDemoDataSeeder> logger)
{
    public async Task SeedAsync(Guid tenantId)
    {
        var (engineeringNotebook, notebookIsNew) = await AddNotebookIfNotExistsAsync(
            tenantId,
            "Engineering",
            "Technical notes, architecture decisions, and implementation details.");

        if (!notebookIsNew)
        {
            logger.LogInformation("Demo notes already seeded, skipping.");
            return;
        }

        var (teamNotebook, _) = await AddNotebookIfNotExistsAsync(
            tenantId,
            "Team Wiki",
            "Shared knowledge base for the team — onboarding, processes, and FAQs.");

        await notesDb.SaveChangesAsync();

        // ── Tags ────────────────────────────────────────────────────────────

        var tagArchitecture = await AddTagIfNotExistsAsync(engineeringNotebook.Id, "architecture", "#6366f1");
        var tagDevOps = await AddTagIfNotExistsAsync(engineeringNotebook.Id, "devops", "#0891b2");
        var tagResearch = await AddTagIfNotExistsAsync(engineeringNotebook.Id, "research", "#d97706");
        var tagOnboarding = await AddTagIfNotExistsAsync(teamNotebook.Id, "onboarding", "#16a34a");
        var tagProcess = await AddTagIfNotExistsAsync(teamNotebook.Id, "process", "#7c3aed");

        await notesDb.SaveChangesAsync();

        // ── Engineering notes ────────────────────────────────────────────────

        var now = DateTime.UtcNow;

        await AddNoteIfNotExistsAsync(
            tenantId, engineeringNotebook.Id, "API Design Guidelines",
            "api-design-guidelines",
            """
            # API Design Guidelines

            ## REST Conventions
            - Use nouns for resource names (e.g. `/api/issues`, not `/api/getIssues`)
            - Use HTTP verbs: GET, POST, PUT, PATCH, DELETE
            - Return 201 Created with a `Location` header for POST requests
            - Use 204 No Content for DELETE operations

            ## Error Responses
            Always return structured errors with a `message` field.

            ## Versioning
            We currently do not version the API. Breaking changes require a deprecation notice of at least 30 days.

            See also: [[Authentication Flow]] [[Database Schema]]
            """,
            NoteStatus.Published, [tagArchitecture.Id], now.AddDays(-14));

        await AddNoteIfNotExistsAsync(
            tenantId, engineeringNotebook.Id, "Authentication Flow",
            "authentication-flow",
            """
            # Authentication Flow

            IssuePit uses **cookie-based authentication** for the browser UI and **API key** auth for agents.

            ## Browser Login
            1. POST `/api/auth/login` with `{ username, password }`
            2. Server sets an `HttpOnly` session cookie
            3. All subsequent requests include the cookie automatically

            ## Tenant Resolution
            The tenant is resolved from the `X-Tenant-Id` header, an MCP bearer token, or by matching the request hostname.

            ## Notes API
            The Notes API is a separate service. It uses the `X-Tenant-Id` header for tenant isolation.
            The frontend obtains the tenant ID from `/api/auth/me` and passes it as `X-Tenant-Id` on every notes request.

            See also: [[API Design Guidelines]]
            """,
            NoteStatus.Published, [tagArchitecture.Id], now.AddDays(-10));

        await AddNoteIfNotExistsAsync(
            tenantId, engineeringNotebook.Id, "Deployment Runbook",
            "deployment-runbook",
            """
            # Deployment Runbook

            ## Pre-deployment checklist
            - [ ] All tests pass in CI
            - [ ] Migration scripts reviewed
            - [ ] Rollback plan documented

            ## Steps
            1. Tag the release in git: `git tag v1.x.x && git push --tags`
            2. CI pipeline builds and pushes Docker images
            3. Aspire orchestration deploys services in dependency order
            4. Run smoke tests against production endpoint

            ## Rollback
            Revert the tag and trigger a re-deploy of the previous image.
            """,
            NoteStatus.Draft, [tagDevOps.Id], now.AddDays(-5));

        await AddNoteIfNotExistsAsync(
            tenantId, engineeringNotebook.Id, "CRDT Research Notes",
            "crdt-research-notes",
            """
            # CRDT / OT Research Notes

            ## Operational Transformation
            We use an OT (Operational Transformation) engine for collaborative note editing.
            Operations are `retain`, `insert`, and `delete` actions applied to a linear document.

            ## Server-side Transform
            The server holds the canonical operation log. Concurrent operations from different clients
            are transformed against each other before being applied.

            ## Delta Format
            Deltas are serialised as JSON arrays: `[{"retain":5},{"insert":"hello"},{"delete":3}]`

            ## References
            - "Operational Transformation in Real-time Group Editors" (Ellis & Gibbs, 1989)
            - Quill Delta spec: https://quilljs.com/docs/delta/
            """,
            NoteStatus.Draft, [tagResearch.Id], now.AddDays(-2));

        // ── Team Wiki notes ─────────────────────────────────────────────────

        await AddNoteIfNotExistsAsync(
            tenantId, teamNotebook.Id, "New Hire Onboarding",
            "new-hire-onboarding",
            """
            # New Hire Onboarding

            Welcome to the team! 🎉

            ## First Week
            1. Set up your development environment (see [[Development Setup]])
            2. Read through the [[API Design Guidelines]]
            3. Get access to Slack, GitHub, and the staging environment
            4. Pair with a buddy for your first task

            ## Tools We Use
            - **IssuePit** — issue tracking and project management
            - **GitHub** — code hosting and CI/CD
            - **Slack** — team communication
            """,
            NoteStatus.Published, [tagOnboarding.Id], now.AddDays(-20));

        await AddNoteIfNotExistsAsync(
            tenantId, teamNotebook.Id, "Development Setup",
            "development-setup",
            """
            # Development Setup

            ## Prerequisites
            - .NET 10 SDK
            - Node.js 20+
            - Docker Desktop
            - Aspire workload: `dotnet workload install aspire`

            ## Running Locally
            ```bash
            cd src/IssuePit.AppHost
            dotnet run
            ```

            The Aspire dashboard will open at http://localhost:15888.

            ## Running Tests
            ```bash
            dotnet test src/IssuePit.slnx --filter "Category=E2E|Category=Smoke"
            ```
            """,
            NoteStatus.Published, [tagOnboarding.Id, tagProcess.Id], now.AddDays(-18));

        await AddNoteIfNotExistsAsync(
            tenantId, teamNotebook.Id, "Code Review Process",
            "code-review-process",
            """
            # Code Review Process

            ## Opening a PR
            1. Keep PRs small and focused (< 400 lines changed when possible)
            2. Link to the relevant IssuePit issue
            3. Add screenshots for UI changes
            4. Ensure CI passes before requesting review

            ## Reviewing
            - Be kind and constructive
            - Distinguish between blocking issues and suggestions (prefix with `nit:`)
            - Approve only when you are confident the change is correct

            ## Merging
            - Require at least one approval
            - Squash-merge feature branches; merge-commit for releases
            """,
            NoteStatus.Published, [tagProcess.Id], now.AddDays(-12));

        await notesDb.SaveChangesAsync();
        logger.LogInformation("Seeded demo notes for tenant {TenantId}.", tenantId);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<(Notebook notebook, bool isNew)> AddNotebookIfNotExistsAsync(
        Guid tenantId, string name, string? description)
    {
        var existing = await notesDb.Notebooks
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Name == name);
        if (existing is not null)
            return (existing, false);

        var notebook = new Notebook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Description = description,
            StorageProvider = StorageProvider.Postgres,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        notesDb.Notebooks.Add(notebook);
        return (notebook, true);
    }

    private async Task<NoteTag> AddTagIfNotExistsAsync(Guid notebookId, string name, string color)
    {
        var existing = await notesDb.NoteTags
            .FirstOrDefaultAsync(t => t.NotebookId == notebookId && t.Name == name);
        if (existing is not null)
            return existing;

        var tag = new NoteTag
        {
            Id = Guid.NewGuid(),
            NotebookId = notebookId,
            Name = name,
            Color = color,
        };
        notesDb.NoteTags.Add(tag);
        return tag;
    }

    private async Task AddNoteIfNotExistsAsync(
        Guid tenantId,
        Guid notebookId,
        string title,
        string slug,
        string content,
        NoteStatus status,
        IReadOnlyList<Guid> tagIds,
        DateTime createdAt)
    {
        if (await notesDb.Notes.AnyAsync(n => n.NotebookId == notebookId && n.Slug == slug))
            return;

        var note = new Note
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            NotebookId = notebookId,
            Title = title,
            Slug = slug,
            Content = content.Trim(),
            Status = status,
            Version = 1,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
        notesDb.Notes.Add(note);

        foreach (var tagId in tagIds)
        {
            notesDb.NoteTagMappings.Add(new NoteTagMapping
            {
                NoteId = note.Id,
                TagId = tagId,
            });
        }
    }
}
