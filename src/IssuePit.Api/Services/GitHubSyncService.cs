using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Handles GitHub ↔ IssuePit issue synchronisation: import, auto-create, and conflict detection.
/// </summary>
public class GitHubSyncService(
    IssuePitDbContext db,
    IDataProtectionProvider dpProvider,
    IHttpClientFactory httpClientFactory,
    ILogger<GitHubSyncService> logger)
{
    private const string ProtectorPurpose = "GitHubOAuthToken";

    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Imports all open (and closed) GitHub issues for the given project.
    /// Creates a <see cref="GitHubSyncRun"/> audit record and appends log entries throughout.
    /// </summary>
    public async Task<GitHubSyncRun> ImportFromGitHubAsync(Guid projectId, CancellationToken ct = default)
    {
        var run = await CreateRunAsync(projectId, ct);

        try
        {
            await SetRunStatusAsync(run, GitHubSyncRunStatus.Running, ct);

            var (token, repo) = await ResolveTokenAndRepoAsync(projectId, run, ct);
            if (token is null || repo is null)
            {
                await FailRunAsync(run, "Sync configuration incomplete — set a GitHub identity and repository.", ct);
                return run;
            }

            await AppendLogAsync(run, GitHubSyncLogLevel.Info, $"Importing issues from {repo}…", ct);

            var ghIssues = await FetchAllGitHubIssuesAsync(token, repo, run, ct);
            await AppendLogAsync(run, GitHubSyncLogLevel.Info, $"Fetched {ghIssues.Count} issue(s) from GitHub.", ct);

            int imported = 0, updated = 0, skipped = 0;

            foreach (var ghIssue in ghIssues)
            {
                var existing = await db.Issues
                    .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.GitHubIssueNumber == ghIssue.Number, ct);

                if (existing is null)
                {
                    // Determine the next sequential issue number.
                    var maxNumber = await db.Issues
                        .Where(i => i.ProjectId == projectId)
                        .MaxAsync(i => (int?)i.Number, ct) ?? 0;

                    var issue = new Issue
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        Number = maxNumber + 1,
                        Title = ghIssue.Title,
                        Body = ghIssue.Body,
                        Status = MapGitHubState(ghIssue.State),
                        Priority = IssuePriority.NoPriority,
                        Type = IssueType.Issue,
                        GitHubIssueNumber = ghIssue.Number,
                        GitHubIssueUrl = ghIssue.HtmlUrl,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    };
                    db.Issues.Add(issue);
                    imported++;
                    await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                        $"  Imported: #{ghIssue.Number} \"{TruncateTitle(ghIssue.Title)}\"", ct);
                }
                else
                {
                    // Only update the GitHub URL / number in case it was missing.
                    bool changed = false;
                    if (existing.GitHubIssueUrl != ghIssue.HtmlUrl)
                    {
                        existing.GitHubIssueUrl = ghIssue.HtmlUrl;
                        changed = true;
                    }
                    if (changed)
                    {
                        existing.UpdatedAt = DateTime.UtcNow;
                        updated++;
                        await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                            $"  Updated link: #{ghIssue.Number} \"{TruncateTitle(ghIssue.Title)}\"", ct);
                    }
                    else
                    {
                        skipped++;
                    }
                }
            }

            await db.SaveChangesAsync(ct);

            run.Summary = $"{imported} imported, {updated} updated, {skipped} unchanged";
            await CompleteRunAsync(run, ct);
            await AppendLogAsync(run, GitHubSyncLogLevel.Info, $"Done. {run.Summary}", ct);
        }
        catch (OperationCanceledException)
        {
            await FailRunAsync(run, "Sync was cancelled.", CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error during GitHub sync import for project {ProjectId}", projectId);
            await FailRunAsync(run, $"Unexpected error: {ex.Message}", CancellationToken.None);
        }

        return run;
    }

    /// <summary>
    /// Returns issues that exist in both GitHub and IssuePit (matched by <c>GitHubIssueNumber</c>)
    /// but have diverging title or body.
    /// </summary>
    public async Task<List<GitHubConflict>> GetConflictsAsync(Guid projectId, CancellationToken ct = default)
    {
        var (token, repo) = await ResolveTokenAndRepoAsync(projectId, null, ct);
        if (token is null || repo is null)
            return [];

        var ghIssues = await FetchAllGitHubIssuesAsync(token, repo, null, ct);
        var ghMap = ghIssues.ToDictionary(i => i.Number);

        var localIssues = await db.Issues
            .Where(i => i.ProjectId == projectId && i.GitHubIssueNumber != null)
            .ToListAsync(ct);

        var conflicts = new List<GitHubConflict>();
        foreach (var local in localIssues)
        {
            if (!ghMap.TryGetValue(local.GitHubIssueNumber!.Value, out var gh))
                continue;

            bool titleDiffers = !string.Equals(local.Title, gh.Title, StringComparison.Ordinal);
            bool bodyDiffers = !string.Equals(local.Body ?? string.Empty, gh.Body ?? string.Empty, StringComparison.Ordinal);

            if (titleDiffers || bodyDiffers)
            {
                conflicts.Add(new GitHubConflict(
                    IssueId: local.Id,
                    IssueNumber: local.Number,
                    GitHubIssueNumber: gh.Number,
                    LocalTitle: local.Title,
                    GitHubTitle: gh.Title,
                    LocalBody: local.Body,
                    GitHubBody: gh.Body,
                    GitHubUrl: gh.HtmlUrl,
                    TitleDiffers: titleDiffers,
                    BodyDiffers: bodyDiffers));
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Creates a new GitHub issue from an existing IssuePit issue and links them.
    /// Called when <see cref="GitHubSyncConfig.AutoCreateOnGitHub"/> is <c>true</c>.
    /// Silently skips if the issue already has a GitHub issue number.
    /// </summary>
    public async Task AutoCreateOnGitHubAsync(Issue issue, CancellationToken ct = default)
    {
        if (issue.GitHubIssueNumber.HasValue)
            return;

        var config = await db.GitHubSyncConfigs
            .Include(c => c.GitHubIdentity)
            .FirstOrDefaultAsync(c => c.ProjectId == issue.ProjectId && c.AutoCreateOnGitHub, ct);

        if (config?.GitHubIdentity is null || string.IsNullOrWhiteSpace(config.GitHubRepo))
            return;

        var token = DecryptToken(config.GitHubIdentity.EncryptedToken);
        if (token is null)
            return;

        try
        {
            var (owner, repo) = ParseRepo(config.GitHubRepo);
            var client = CreateHttpClient(token);

            var payload = JsonSerializer.Serialize(new { title = issue.Title, body = issue.Body ?? string.Empty });
            var response = await client.PostAsync(
                $"https://api.github.com/repos/{owner}/{repo}/issues",
                new StringContent(payload, Encoding.UTF8, "application/json"), ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to create GitHub issue for {IssueId}: {Status}", issue.Id, response.StatusCode);
                return;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            issue.GitHubIssueNumber = json.GetProperty("number").GetInt32();
            issue.GitHubIssueUrl = json.GetProperty("html_url").GetString();
            issue.UpdatedAt = DateTime.UtcNow;
            // Caller is responsible for SaveChangesAsync.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error auto-creating GitHub issue for {IssueId}", issue.Id);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<GitHubSyncRun> CreateRunAsync(Guid projectId, CancellationToken ct)
    {
        var run = new GitHubSyncRun
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Status = GitHubSyncRunStatus.Pending,
            StartedAt = DateTime.UtcNow,
        };
        db.GitHubSyncRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    private async Task SetRunStatusAsync(GitHubSyncRun run, GitHubSyncRunStatus status, CancellationToken ct)
    {
        run.Status = status;
        await db.SaveChangesAsync(ct);
    }

    private async Task AppendLogAsync(GitHubSyncRun? run, GitHubSyncLogLevel level, string message, CancellationToken ct)
    {
        logger.LogInformation("[GitHubSync] [{Level}] {Message}", level, message);
        if (run is null) return;
        db.GitHubSyncRunLogs.Add(new GitHubSyncRunLog
        {
            Id = Guid.NewGuid(),
            SyncRunId = run.Id,
            Level = level,
            Message = message,
            Timestamp = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task CompleteRunAsync(GitHubSyncRun run, CancellationToken ct)
    {
        run.Status = GitHubSyncRunStatus.Succeeded;
        run.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task FailRunAsync(GitHubSyncRun run, string reason, CancellationToken ct)
    {
        run.Status = GitHubSyncRunStatus.Failed;
        run.CompletedAt = DateTime.UtcNow;
        run.Summary = reason;
        db.GitHubSyncRunLogs.Add(new GitHubSyncRunLog
        {
            Id = Guid.NewGuid(),
            SyncRunId = run.Id,
            Level = GitHubSyncLogLevel.Error,
            Message = reason,
            Timestamp = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<(string? token, string? repo)> ResolveTokenAndRepoAsync(
        Guid projectId, GitHubSyncRun? run, CancellationToken ct)
    {
        var config = await db.GitHubSyncConfigs
            .Include(c => c.GitHubIdentity)
            .FirstOrDefaultAsync(c => c.ProjectId == projectId, ct);

        if (config is null)
        {
            await AppendLogAsync(run, GitHubSyncLogLevel.Warn, "No sync configuration found for this project.", ct);
            return (null, null);
        }

        if (config.GitHubIdentity is null)
        {
            await AppendLogAsync(run, GitHubSyncLogLevel.Warn, "No GitHub identity linked to sync configuration.", ct);
            return (null, null);
        }

        if (string.IsNullOrWhiteSpace(config.GitHubRepo))
        {
            await AppendLogAsync(run, GitHubSyncLogLevel.Warn, "No GitHub repository configured.", ct);
            return (null, null);
        }

        if (!config.GitHubRepo.Contains('/'))
        {
            await AppendLogAsync(run, GitHubSyncLogLevel.Error,
                $"GitHub repository \"{config.GitHubRepo}\" is not in the required owner/repo format.", ct);
            return (null, null);
        }

        var token = DecryptToken(config.GitHubIdentity.EncryptedToken);
        if (token is null)
        {
            await AppendLogAsync(run, GitHubSyncLogLevel.Error, "Failed to decrypt GitHub token.", ct);
            return (null, null);
        }

        return (token, config.GitHubRepo);
    }

    private string? DecryptToken(string encryptedToken)
    {
        try
        {
            var protector = dpProvider.CreateProtector(ProtectorPurpose);
            return protector.Unprotect(encryptedToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decrypt GitHub token");
            return null;
        }
    }

    private async Task<List<GitHubIssueDto>> FetchAllGitHubIssuesAsync(
        string token, string repo, GitHubSyncRun? run, CancellationToken ct)
    {
        var (owner, repoName) = ParseRepo(repo);
        var client = CreateHttpClient(token);

        var allIssues = new List<GitHubIssueDto>();
        var page = 1;

        while (true)
        {
            var url = $"https://api.github.com/repos/{owner}/{repoName}/issues?state=all&per_page=100&page={page}";
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                await AppendLogAsync(run, GitHubSyncLogLevel.Error,
                    $"GitHub API returned {(int)response.StatusCode} for page {page}.", ct);
                break;
            }

            var issues = await response.Content.ReadFromJsonAsync<List<GitHubIssueDto>>(ct) ?? [];

            // Filter out pull requests (GitHub returns them alongside issues).
            var onlyIssues = issues.Where(i => i.PullRequest is null).ToList();
            allIssues.AddRange(onlyIssues);

            if (issues.Count < 100)
                break;

            page++;
        }

        return allIssues;
    }

    private HttpClient CreateHttpClient(string token)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("IssuePit/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static (string owner, string repo) ParseRepo(string ownerRepo)
    {
        var parts = ownerRepo.Split('/', 2);
        if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
            throw new ArgumentException($"Invalid GitHub repository format: \"{ownerRepo}\". Expected owner/repo.", nameof(ownerRepo));
        return (parts[0], parts[1]);
    }

    private static IssueStatus MapGitHubState(string? state) =>
        state?.ToLowerInvariant() == "closed" ? IssueStatus.Done : IssueStatus.Backlog;

    private static string TruncateTitle(string title, int max = 60) =>
        title.Length <= max ? title : title[..max] + "…";

    // ──────────────────────────────────────────────────────────────────────────
    // DTO types
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class GitHubIssueDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("number")]
        public int Number { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("body")]
        public string? Body { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("state")]
        public string? State { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("pull_request")]
        public object? PullRequest { get; set; }
    }
}

/// <summary>Represents an issue that exists in both systems with divergent content.</summary>
public record GitHubConflict(
    Guid IssueId,
    int IssueNumber,
    int GitHubIssueNumber,
    string LocalTitle,
    string GitHubTitle,
    string? LocalBody,
    string? GitHubBody,
    string GitHubUrl,
    bool TitleDiffers,
    bool BodyDiffers);
