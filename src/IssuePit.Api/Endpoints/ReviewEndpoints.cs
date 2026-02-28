using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IssuePit.Api.Endpoints;

public static class ReviewEndpoints
{
    // A file with more than this many total diff lines is considered "big"
    private const int BigFileThreshold = 300;

    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        // --- PR Diff ---

        app.MapGet("/api/issues/{id:guid}/pr-diff", async (
            Guid id,
            string? @base,
            IssuePitDbContext db,
            TenantContext tenant,
            IHttpClientFactory httpClientFactory) =>
        {
            if (tenant.CurrentTenant is null) return Results.Unauthorized();

            var issue = await db.Issues
                .Include(i => i.Project)
                .ThenInclude(p => p.Organization)
                .FirstOrDefaultAsync(i => i.Id == id && i.Project.Organization.TenantId == tenant.CurrentTenant.Id);

            if (issue is null) return Results.NotFound();
            if (string.IsNullOrEmpty(issue.GitBranch))
                return Results.BadRequest(new { error = "Issue has no git branch set." });
            if (string.IsNullOrEmpty(issue.Project.GitHubRepo))
                return Results.BadRequest(new { error = "Project has no GitHub repository configured." });

            // Resolve GitHub API key for the tenant
            var githubKey = await db.ApiKeys
                .Where(k => k.Organization.TenantId == tenant.CurrentTenant.Id && k.Provider == ApiKeyProvider.GitHub)
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => k.EncryptedValue)
                .FirstOrDefaultAsync();

            var token = githubKey?.StartsWith("plain:") == true ? githubKey[6..] : githubKey;
            var baseBranch = @base ?? "main";
            var repo = issue.Project.GitHubRepo; // expected: "owner/repo"
            var head = issue.GitBranch;

            var http = httpClientFactory.CreateClient("github");
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{repo}/compare/{baseBranch}...{head}");

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage ghResponse;
            try
            {
                ghResponse = await http.SendAsync(request);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to reach GitHub API: {ex.Message}");
            }

            if (!ghResponse.IsSuccessStatusCode)
            {
                var body = await ghResponse.Content.ReadAsStringAsync();
                return Results.Problem($"GitHub API returned {(int)ghResponse.StatusCode}: {body}");
            }

            var json = await ghResponse.Content.ReadAsStringAsync();
            var compare = JsonSerializer.Deserialize<GitHubCompareResponse>(json, JsonOpts);
            if (compare is null) return Results.Problem("Failed to parse GitHub response.");

            var files = (compare.Files ?? []).Select(f =>
            {
                var hunks = DiffParser.ParsePatch(f.Patch);
                var totalDiffLines = hunks.Sum(h => h.Lines.Count);
                return new DiffFileDto(
                    Filename: f.Filename ?? string.Empty,
                    Status: f.Status ?? "modified",
                    Additions: f.Additions,
                    Deletions: f.Deletions,
                    Changes: f.Changes,
                    IsBig: totalDiffLines > BigFileThreshold,
                    // Return hunks only for non-big files; big files require explicit load
                    Hunks: totalDiffLines > BigFileThreshold ? [] : hunks
                );
            }).ToList();

            return Results.Ok(new PrDiffDto(
                BaseBranch: baseBranch,
                HeadBranch: head,
                TotalAdditions: files.Sum(f => f.Additions),
                TotalDeletions: files.Sum(f => f.Deletions),
                Files: files
            ));
        });

        // Load hunks for a single big file
        app.MapGet("/api/issues/{id:guid}/pr-diff/file", async (
            Guid id,
            string? @base,
            string filename,
            IssuePitDbContext db,
            TenantContext tenant,
            IHttpClientFactory httpClientFactory) =>
        {
            if (tenant.CurrentTenant is null) return Results.Unauthorized();

            var issue = await db.Issues
                .Include(i => i.Project)
                .ThenInclude(p => p.Organization)
                .FirstOrDefaultAsync(i => i.Id == id && i.Project.Organization.TenantId == tenant.CurrentTenant.Id);

            if (issue is null) return Results.NotFound();
            if (string.IsNullOrEmpty(issue.GitBranch) || string.IsNullOrEmpty(issue.Project.GitHubRepo))
                return Results.BadRequest(new { error = "Issue has no git branch or project has no GitHub repository." });

            var githubKey = await db.ApiKeys
                .Where(k => k.Organization.TenantId == tenant.CurrentTenant.Id && k.Provider == ApiKeyProvider.GitHub)
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => k.EncryptedValue)
                .FirstOrDefaultAsync();

            var token = githubKey?.StartsWith("plain:") == true ? githubKey[6..] : githubKey;
            var baseBranch = @base ?? "main";
            var repo = issue.Project.GitHubRepo;
            var head = issue.GitBranch;

            var http = httpClientFactory.CreateClient("github");
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{repo}/compare/{baseBranch}...{head}");

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var ghResponse = await http.SendAsync(request);
            if (!ghResponse.IsSuccessStatusCode)
                return Results.Problem($"GitHub API returned {(int)ghResponse.StatusCode}");

            var json = await ghResponse.Content.ReadAsStringAsync();
            var compare = JsonSerializer.Deserialize<GitHubCompareResponse>(json, JsonOpts);
            var file = compare?.Files?.FirstOrDefault(f => f.Filename == filename);
            if (file is null) return Results.NotFound();

            return Results.Ok(DiffParser.ParsePatch(file.Patch));
        });

        // --- Comments ---

        app.MapGet("/api/issues/{id:guid}/comments", async (Guid id, IssuePitDbContext db, TenantContext tenant) =>
        {
            if (tenant.CurrentTenant is null) return Results.Unauthorized();

            var comments = await db.IssueComments
                .Where(c => c.IssueId == id)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.IssueId,
                    c.FilePath,
                    c.LineNumber,
                    c.EndLineNumber,
                    c.Body,
                    c.CommentType,
                    c.AuthorName,
                    c.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(comments);
        });

        app.MapPost("/api/issues/{id:guid}/comments", async (
            Guid id,
            IssueCommentRequest req,
            IssuePitDbContext db,
            TenantContext tenant) =>
        {
            if (tenant.CurrentTenant is null) return Results.Unauthorized();

            var issueExists = await db.Issues
                .Include(i => i.Project)
                .ThenInclude(p => p.Organization)
                .AnyAsync(i => i.Id == id && i.Project.Organization.TenantId == tenant.CurrentTenant.Id);

            if (!issueExists) return Results.NotFound();

            var comment = new IssueComment
            {
                Id = Guid.NewGuid(),
                IssueId = id,
                FilePath = req.FilePath,
                LineNumber = req.LineNumber,
                EndLineNumber = req.EndLineNumber,
                Body = req.Body,
                CommentType = req.CommentType ?? "comment",
                AuthorName = req.AuthorName,
                CreatedAt = DateTime.UtcNow
            };

            db.IssueComments.Add(comment);
            await db.SaveChangesAsync();

            return Results.Created($"/api/issues/{id}/comments/{comment.Id}", new
            {
                comment.Id,
                comment.IssueId,
                comment.FilePath,
                comment.LineNumber,
                comment.EndLineNumber,
                comment.Body,
                comment.CommentType,
                comment.AuthorName,
                comment.CreatedAt
            });
        });

        app.MapDelete("/api/issues/{id:guid}/comments/{commentId:guid}", async (
            Guid id,
            Guid commentId,
            IssuePitDbContext db,
            TenantContext tenant) =>
        {
            if (tenant.CurrentTenant is null) return Results.Unauthorized();

            var comment = await db.IssueComments
                .Include(c => c.Issue)
                .ThenInclude(i => i.Project)
                .ThenInclude(p => p.Organization)
                .FirstOrDefaultAsync(c => c.Id == commentId && c.IssueId == id &&
                    c.Issue.Project.Organization.TenantId == tenant.CurrentTenant.Id);

            if (comment is null) return Results.NotFound();

            db.IssueComments.Remove(comment);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // --- Request / Response DTOs ---

    private record IssueCommentRequest(
        string Body,
        string? FilePath = null,
        int? LineNumber = null,
        int? EndLineNumber = null,
        string? CommentType = null,
        string? AuthorName = null);

    // GitHub Compare API response shapes
    private class GitHubCompareResponse
    {
        [JsonPropertyName("files")]
        public List<GitHubFile>? Files { get; set; }
    }

    private class GitHubFile
    {
        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("additions")]
        public int Additions { get; set; }

        [JsonPropertyName("deletions")]
        public int Deletions { get; set; }

        [JsonPropertyName("changes")]
        public int Changes { get; set; }

        [JsonPropertyName("patch")]
        public string? Patch { get; set; }
    }
}

// --- Public response DTOs used by the endpoint ---

public record DiffFileDto(
    string Filename,
    string Status,
    int Additions,
    int Deletions,
    int Changes,
    bool IsBig,
    List<DiffHunkDto> Hunks);

public record PrDiffDto(
    string BaseBranch,
    string HeadBranch,
    int TotalAdditions,
    int TotalDeletions,
    List<DiffFileDto> Files);
