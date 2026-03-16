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
/// Handles GitHub ↔ IssuePit issue synchronisation: import, two-way sync, and auto-create.
/// The direction of sync is controlled by <see cref="GitHubSyncConfig.SyncMode"/>.
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
    /// Runs a sync for the given project according to its configured <see cref="GitHubSyncMode"/>:
    /// Import (GitHub→IssuePit), TwoWay (bidirectional), or CreateOnGitHub (no batch import).
    /// Creates a <see cref="GitHubSyncRun"/> audit record with full log output.
    /// </summary>
    public async Task<GitHubSyncRun> SyncAsync(Guid projectId, CancellationToken ct = default)
    {
        var run = await CreateRunAsync(projectId, ct);

        try
        {
            await SetRunStatusAsync(run, GitHubSyncRunStatus.Running, ct);

            var config = await db.GitHubSyncConfigs
                .Include(c => c.GitHubIdentity)
                .FirstOrDefaultAsync(c => c.ProjectId == projectId, ct);

            if (config?.SyncMode == GitHubSyncMode.CreateOnGitHub)
            {
                await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                    "SyncMode is CreateOnGitHub — batch sync is not applicable. Issues are pushed to GitHub individually when created.", ct);
                await CompleteRunAsync(run, ct);
                return run;
            }

            var (token, repo) = await ResolveTokenAndRepoAsync(projectId, run, ct);
            if (token is null || repo is null)
            {
                await FailRunAsync(run, "Sync configuration incomplete — set a GitHub identity and repository.", ct);
                return run;
            }

            var syncMode = config?.SyncMode ?? GitHubSyncMode.Import;
            await AppendLogAsync(run, GitHubSyncLogLevel.Info, $"SyncMode = {syncMode}. Fetching issues from {repo}...", ct);

            bool fetchError = false;
            var ghIssues = await FetchAllGitHubIssuesAsync(token, repo, run, ct, out fetchError);
            await AppendLogAsync(run, GitHubSyncLogLevel.Info, $"Fetched {ghIssues.Count} issue(s) from GitHub.", ct);

            if (fetchError && ghIssues.Count == 0)
            {
                await FailRunAsync(run, "Failed to fetch issues from GitHub (check token and repository settings).", ct);
                return run;
            }

            // Sort ascending so oldest issues (lowest GitHub numbers) get imported first
            // → they receive the lowest IssuePit numbers
            ghIssues = ghIssues.OrderBy(i => i.Number).ToList();

            // Compute the current max number once; increment in-memory to avoid per-save race conditions
            var maxNumber = await db.Issues
                .Where(i => i.ProjectId == projectId)
                .MaxAsync(i => (int?)i.Number, ct) ?? 0;

            int imported = 0, updated = 0, pushed = 0, skipped = 0;

            foreach (var ghIssue in ghIssues)
            {
                var existing = await db.Issues
                    .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.GitHubIssueNumber == ghIssue.Number, ct);

                if (existing is null)
                {
                    maxNumber++;
                    db.Issues.Add(new Issue
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        Number = maxNumber,
                        Title = ghIssue.Title,
                        Body = ghIssue.Body,
                        Status = MapGitHubState(ghIssue.State),
                        Priority = IssuePriority.NoPriority,
                        Type = IssueType.Issue,
                        GitHubIssueNumber = ghIssue.Number,
                        GitHubIssueUrl = ghIssue.HtmlUrl,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    });
                    imported++;
                    await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                        $"  Imported: {repo}#{ghIssue.Number} \"{TruncateTitle(ghIssue.Title)}\"", ct);
                }
                else if (syncMode == GitHubSyncMode.TwoWay)
                {
                    // Two-way: if the IssuePit issue was modified very recently, push it to GitHub.
                    // Otherwise pull from GitHub as the authoritative source.
                    bool localNewer = existing.UpdatedAt > DateTime.UtcNow.AddMinutes(-5);
                    bool contentDiffers =
                        !string.Equals(existing.Title, ghIssue.Title, StringComparison.Ordinal) ||
                        !string.Equals(existing.Body ?? string.Empty, ghIssue.Body ?? string.Empty, StringComparison.Ordinal);

                    if (localNewer && contentDiffers)
                    {
                        var pushResult = await PushIssueToGitHubAsync(existing, token, repo, ct);
                        if (pushResult)
                        {
                            pushed++;
                            await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                                $"  Pushed local changes: {repo}#{ghIssue.Number} \"{TruncateTitle(existing.Title)}\"", ct);
                        }
                        else
                        {
                            await AppendLogAsync(run, GitHubSyncLogLevel.Warn,
                                $"  Failed to push: {repo}#{ghIssue.Number} \"{TruncateTitle(existing.Title)}\"", ct);
                        }
                    }
                    else
                    {
                        bool changed = false;
                        if (!string.Equals(existing.Title, ghIssue.Title, StringComparison.Ordinal))
                        {
                            existing.Title = ghIssue.Title;
                            changed = true;
                        }
                        if (!string.Equals(existing.Body ?? string.Empty, ghIssue.Body ?? string.Empty, StringComparison.Ordinal))
                        {
                            existing.Body = ghIssue.Body;
                            changed = true;
                        }
                        if (!string.Equals(existing.GitHubIssueUrl, ghIssue.HtmlUrl, StringComparison.Ordinal))
                        {
                            existing.GitHubIssueUrl = ghIssue.HtmlUrl;
                            changed = true;
                        }
                        if (changed)
                        {
                            existing.UpdatedAt = DateTime.UtcNow;
                            updated++;
                            await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                                $"  Updated from GitHub: {repo}#{ghIssue.Number} \"{TruncateTitle(ghIssue.Title)}\"", ct);
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                }
                else
                {
                    // Import mode: only patch stale GitHub URL links.
                    if (!string.Equals(existing.GitHubIssueUrl, ghIssue.HtmlUrl, StringComparison.Ordinal))
                    {
                        existing.GitHubIssueUrl = ghIssue.HtmlUrl;
                        existing.UpdatedAt = DateTime.UtcNow;
                        updated++;
                        await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                            $"  Updated link: {repo}#{ghIssue.Number} \"{TruncateTitle(ghIssue.Title)}\"", ct);
                    }
                    else
                    {
                        skipped++;
                    }
                }
            }

            await db.SaveChangesAsync(ct);

            run.Summary = syncMode == GitHubSyncMode.TwoWay
                ? $"{imported} imported, {updated} updated from GitHub, {pushed} pushed to GitHub, {skipped} unchanged"
                : $"{imported} imported, {updated} updated, {skipped} unchanged";

            // Import GitHub pull requests as IssuePit merge requests
            await ImportPullRequestsAsync(token, repo, projectId, run, ct);

            await CompleteRunAsync(run, ct);
            await AppendLogAsync(run, GitHubSyncLogLevel.Info, $"Done. {run.Summary}", ct);
        }
        catch (OperationCanceledException)
        {
            await FailRunAsync(run, "Sync was cancelled.", CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error during GitHub sync for project {ProjectId}", projectId);
            await FailRunAsync(run, $"Unexpected error: {ex.Message}", CancellationToken.None);
        }

        return run;
    }

    /// <summary>
    /// Returns issues that exist in both GitHub and IssuePit (matched by GitHubIssueNumber)
    /// but have diverging title or body.
    /// </summary>
    public async Task<List<GitHubConflict>> GetConflictsAsync(Guid projectId, CancellationToken ct = default)
    {
        var (token, repo) = await ResolveTokenAndRepoAsync(projectId, null, ct);
        if (token is null || repo is null)
            return [];

        bool fetchError;
        var ghIssues = await FetchAllGitHubIssuesAsync(token, repo, null, ct, out fetchError);
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
    /// Only operates when the project's SyncMode is CreateOnGitHub.
    /// Silently skips if the issue already has a GitHub issue number.
    /// </summary>
    public async Task AutoCreateOnGitHubAsync(Issue issue, CancellationToken ct = default)
    {
        if (issue.GitHubIssueNumber.HasValue)
            return;

        var config = await db.GitHubSyncConfigs
            .Include(c => c.GitHubIdentity)
            .FirstOrDefaultAsync(c => c.ProjectId == issue.ProjectId && c.SyncMode == GitHubSyncMode.CreateOnGitHub, ct);

        if (config?.GitHubIdentity is null || string.IsNullOrWhiteSpace(config.GitHubRepo))
            return;

        var token = DecryptToken(config.GitHubIdentity.EncryptedToken);
        if (token is null)
            return;

        try
        {
            var (owner, repo) = ParseRepo(NormalizeRepo(config.GitHubRepo));
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

    /// <summary>
    /// Imports GitHub pull requests as IssuePit merge requests.
    /// Skips PRs that have already been imported (matched by GitHubPrNumber).
    /// No CI/CD run is triggered for imported PRs.
    /// </summary>
    private async Task ImportPullRequestsAsync(string token, string repo, Guid projectId, GitHubSyncRun? run, CancellationToken ct)
    {
        bool prFetchError;
        var prs = await FetchAllGitHubPullRequestsAsync(token, repo, run, ct, out prFetchError);
        if (prs.Count == 0) return;

        await AppendLogAsync(run, GitHubSyncLogLevel.Info, $"Fetched {prs.Count} pull request(s) from GitHub.", ct);

        // Sort ascending so oldest PRs get imported first
        prs = prs.OrderBy(p => p.Number).ToList();

        int prImported = 0, prSkipped = 0;
        foreach (var pr in prs)
        {
            var existing = await db.MergeRequests
                .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.GitHubPrNumber == pr.Number, ct);
            if (existing is not null)
            {
                prSkipped++;
                continue;
            }

            var status = pr.MergedAt.HasValue
                ? MergeRequestStatus.Merged
                : pr.State?.ToLowerInvariant() == "closed"
                    ? MergeRequestStatus.Closed
                    : MergeRequestStatus.Open;

            db.MergeRequests.Add(new MergeRequest
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = pr.Title,
                Description = pr.Body,
                SourceBranch = pr.Head?.Ref ?? "unknown",
                TargetBranch = pr.Base?.Ref ?? "main",
                Status = status,
                AutoMergeEnabled = false,
                GitHubPrNumber = pr.Number,
                GitHubPrUrl = pr.HtmlUrl,
                CreatedAt = pr.CreatedAt,
                UpdatedAt = pr.UpdatedAt,
                MergedAt = pr.MergedAt,
            });
            prImported++;
            await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                $"  Imported PR: {repo}#{pr.Number} \"{TruncateTitle(pr.Title)}\"", ct);
        }

        if (prImported > 0 || prSkipped > 0)
        {
            await db.SaveChangesAsync(ct);
            await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                $"Pull requests: {prImported} imported, {prSkipped} already synced.", ct);
        }
    }

    private async Task<bool> PushIssueToGitHubAsync(Issue issue, string token, string repo, CancellationToken ct)
    {
        try
        {
            if (!issue.GitHubIssueNumber.HasValue) return false;

            var (owner, repoName) = ParseRepo(repo);
            var client = CreateHttpClient(token);

            var payload = JsonSerializer.Serialize(new
            {
                title = issue.Title,
                body = issue.Body ?? string.Empty,
            });

            var response = await client.PatchAsync(
                $"https://api.github.com/repos/{owner}/{repoName}/issues/{issue.GitHubIssueNumber.Value}",
                new StringContent(payload, Encoding.UTF8, "application/json"), ct);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error pushing issue {IssueId} to GitHub", issue.Id);
            return false;
        }
    }

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

        var normalizedRepo = NormalizeRepo(config.GitHubRepo);
        if (!normalizedRepo.Contains('/'))
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

        return (token, normalizedRepo);
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
        string token, string repo, GitHubSyncRun? run, CancellationToken ct, out bool fetchError)
    {
        var (owner, repoName) = ParseRepo(repo);
        var client = CreateHttpClient(token);

        var allIssues = new List<GitHubIssueDto>();
        var page = 1;
        fetchError = false;

        while (true)
        {
            var url = $"https://api.github.com/repos/{owner}/{repoName}/issues?state=all&per_page=100&page={page}";
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                fetchError = true;
                await AppendLogAsync(run, GitHubSyncLogLevel.Error,
                    $"GitHub API returned {(int)response.StatusCode} for issues page {page}.", ct);
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

    private async Task<List<GitHubPullRequestDto>> FetchAllGitHubPullRequestsAsync(
        string token, string repo, GitHubSyncRun? run, CancellationToken ct, out bool fetchError)
    {
        var (owner, repoName) = ParseRepo(repo);
        var client = CreateHttpClient(token);

        var allPrs = new List<GitHubPullRequestDto>();
        var page = 1;
        fetchError = false;

        while (true)
        {
            var url = $"https://api.github.com/repos/{owner}/{repoName}/pulls?state=all&per_page=100&page={page}";
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                fetchError = true;
                await AppendLogAsync(run, GitHubSyncLogLevel.Warn,
                    $"GitHub API returned {(int)response.StatusCode} for pulls page {page}.", ct);
                break;
            }

            var prs = await response.Content.ReadFromJsonAsync<List<GitHubPullRequestDto>>(ct) ?? [];
            allPrs.AddRange(prs);

            if (prs.Count < 100)
                break;

            page++;
        }

        return allPrs;
    }

    private HttpClient CreateHttpClient(string token)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("IssuePit/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>
    /// Strips the <c>https://github.com/</c> prefix from a GitHub URL so that both
    /// <c>owner/repo</c> and <c>https://github.com/owner/repo</c> are accepted.
    /// </summary>
    private static string NormalizeRepo(string ownerRepo)
    {
        const string prefix = "https://github.com/";
        if (ownerRepo.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return ownerRepo[prefix.Length..].TrimEnd('/');
        return ownerRepo.Trim();
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
        title.Length <= max ? title : title[..max] + "...";

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

    private sealed class GitHubPullRequestDto
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

        [System.Text.Json.Serialization.JsonPropertyName("merged_at")]
        public DateTime? MergedAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("head")]
        public GitHubBranchRefDto? Head { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("base")]
        public GitHubBranchRefDto? Base { get; set; }
    }

    private sealed class GitHubBranchRefDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("ref")]
        public string Ref { get; set; } = string.Empty;
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
