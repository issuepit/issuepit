using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Octokit;

namespace IssuePit.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/issues/{issueId:guid}/review");

        // GET /api/issues/{issueId}/review/diff
        // Returns the diff for the issue's git branch vs the default branch.
        group.MapGet("/diff", async (Guid issueId, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();

            var issue = await db.Issues
                .Include(i => i.Project)
                .ThenInclude(p => p.Organization)
                .FirstOrDefaultAsync(i => i.Id == issueId && i.Project.Organization.TenantId == ctx.CurrentTenant.Id);

            if (issue is null) return Results.NotFound();
            if (string.IsNullOrEmpty(issue.GitBranch)) return Results.BadRequest("Issue has no git branch set.");
            if (string.IsNullOrEmpty(issue.Project.GitHubRepo)) return Results.BadRequest("Project has no GitHub repository configured.");

            // Retrieve a GitHub token from the stored API keys (if any)
            var githubKey = await db.ApiKeys
                .Where(k => k.OrgId == issue.Project.OrgId && k.Provider == Core.Enums.ApiKeyProvider.GitHub)
                .Select(k => k.EncryptedValue)
                .FirstOrDefaultAsync();

            var (owner, repo) = ParseGitHubRepo(issue.Project.GitHubRepo);
            if (owner is null || repo is null)
                return Results.BadRequest("Invalid GitHub repository format. Expected 'owner/repo'.");

            try
            {
                var github = new GitHubClient(new ProductHeaderValue("IssuePit"));
                if (!string.IsNullOrEmpty(githubKey))
                    github.Credentials = new Credentials(githubKey);

                // Compare default branch to the issue's branch
                var comparison = await github.Repository.Commit.Compare(owner, repo, "HEAD", issue.GitBranch);

                var diffFiles = comparison.Files.Select(f => new DiffFileDto
                {
                    FileName = f.Filename,
                    Status = f.Status,
                    Additions = f.Additions,
                    Deletions = f.Deletions,
                    Changes = f.Changes,
                    Patch = f.Patch,
                    BlobUrl = f.BlobUrl,
                    RawUrl = f.RawUrl,
                }).ToList();

                return Results.Ok(new DiffResultDto
                {
                    Branch = issue.GitBranch,
                    BaseLabel = comparison.BaseCommit?.Sha ?? string.Empty,
                    HeadLabel = comparison.MergeBaseCommit?.Sha ?? string.Empty,
                    CommitSha = comparison.Commits.LastOrDefault()?.Sha ?? string.Empty,
                    Files = diffFiles,
                    TotalAdditions = diffFiles.Sum(f => f.Additions),
                    TotalDeletions = diffFiles.Sum(f => f.Deletions),
                });
            }
            catch (NotFoundException)
            {
                return Results.NotFound("Branch or repository not found on GitHub.");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to fetch diff: {ex.Message}");
            }
        });

        // GET /api/issues/{issueId}/review/comments
        group.MapGet("/comments", async (Guid issueId, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();

            var issueExists = await db.Issues
                .Include(i => i.Project)
                .ThenInclude(p => p.Organization)
                .AnyAsync(i => i.Id == issueId && i.Project.Organization.TenantId == ctx.CurrentTenant.Id);

            if (!issueExists) return Results.NotFound();

            var comments = await db.IssueComments
                .Where(c => c.IssueId == issueId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            return Results.Ok(comments);
        });

        // POST /api/issues/{issueId}/review/comments
        group.MapPost("/comments", async (Guid issueId, IssuePit.Core.Entities.IssueComment comment, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();

            var issueExists = await db.Issues
                .Include(i => i.Project)
                .ThenInclude(p => p.Organization)
                .AnyAsync(i => i.Id == issueId && i.Project.Organization.TenantId == ctx.CurrentTenant.Id);

            if (!issueExists) return Results.NotFound();

            comment.Id = Guid.NewGuid();
            comment.IssueId = issueId;
            comment.CreatedAt = DateTime.UtcNow;
            comment.UpdatedAt = DateTime.UtcNow;

            db.IssueComments.Add(comment);
            await db.SaveChangesAsync();

            return Results.Created($"/api/issues/{issueId}/review/comments/{comment.Id}", comment);
        });

        // DELETE /api/issues/{issueId}/review/comments/{commentId}
        group.MapDelete("/comments/{commentId:guid}", async (Guid issueId, Guid commentId, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();

            var comment = await db.IssueComments
                .Include(c => c.Issue)
                .ThenInclude(i => i.Project)
                .ThenInclude(p => p.Organization)
                .FirstOrDefaultAsync(c => c.Id == commentId && c.IssueId == issueId
                    && c.Issue.Project.Organization.TenantId == ctx.CurrentTenant.Id);

            if (comment is null) return Results.NotFound();

            db.IssueComments.Remove(comment);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }

    private static (string? Owner, string? Repo) ParseGitHubRepo(string githubRepo)
    {
        // Support formats: "owner/repo" or "https://github.com/owner/repo"
        var trimmed = githubRepo
            .Replace("https://github.com/", string.Empty)
            .Replace("http://github.com/", string.Empty)
            .TrimEnd('/');

        var parts = trimmed.Split('/', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (null, null);
    }
}

public sealed record DiffFileDto
{
    public string FileName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Additions { get; init; }
    public int Deletions { get; init; }
    public int Changes { get; init; }
    public string? Patch { get; init; }
    public string? BlobUrl { get; init; }
    public string? RawUrl { get; init; }
}

public sealed record DiffResultDto
{
    public string Branch { get; init; } = string.Empty;
    public string BaseLabel { get; init; } = string.Empty;
    public string HeadLabel { get; init; } = string.Empty;
    public string CommitSha { get; init; } = string.Empty;
    public List<DiffFileDto> Files { get; init; } = [];
    public int TotalAdditions { get; init; }
    public int TotalDeletions { get; init; }
}
