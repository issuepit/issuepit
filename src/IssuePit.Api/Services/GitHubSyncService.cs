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
/// Which content is synced is controlled by <see cref="GitHubSyncConfig.SyncContent"/>.
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
    /// Runs a sync for the given project according to its configured <see cref="GitHubSyncMode"/> and
    /// <see cref="GitHubSyncContent"/>: Import (GitHub→IssuePit), TwoWay (bidirectional), or
    /// CreateOnGitHub (no batch import). Creates a <see cref="GitHubSyncRun"/> audit record.
    /// </summary>
    public async Task<GitHubSyncRun> SyncAsync(Guid projectId, bool dryRun = false, CancellationToken ct = default)
    {
        var run = await CreateRunAsync(projectId, ct);

        try
        {
            await SetRunStatusAsync(run, GitHubSyncRunStatus.Running, ct);

            if (dryRun)
                await AppendLogAsync(run, GitHubSyncLogLevel.Info, "DRY RUN mode — no changes will be saved.", ct);

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
            var syncContent = config?.SyncContent ?? GitHubSyncContent.Issues;
            await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                $"SyncMode = {syncMode}, SyncContent = {syncContent}. Fetching from {repo}...", ct);

            // ── Issues & Pull Requests ────────────────────────────────────────
            int imported = 0, updated = 0, pushed = 0, skipped = 0;
            int prsImported = 0, prsSkipped = 0;

            if (syncContent.HasFlag(GitHubSyncContent.Issues))
            {
                // Fetch issues — throws GitHubApiException when the API returns an error.
                List<GitHubIssueDto> ghIssues;
                List<GitHubPullRequestDto> ghPrs;
                try
                {
                    ghIssues = await FetchAllGitHubIssuesAsync(token, repo, run, ct);
                    ghPrs = await FetchAllGitHubPullRequestsAsync(token, repo, run, ct);
                }
                catch (GitHubApiException ex)
                {
                    await FailRunAsync(run, ex.Message, ct);
                    return run;
                }

                await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                    $"Fetched {ghIssues.Count} issue(s) and {ghPrs.Count} PR(s) from GitHub.", ct);

                // Import oldest first so the lowest GitHub numbers get the lowest IssuePit numbers.
                ghIssues = [.. ghIssues.OrderBy(i => i.Number)];
                ghPrs = [.. ghPrs.OrderBy(p => p.Number)];

                // Compute starting number once (avoids N separate MAX queries inside the loop).
                var nextNumber = (await db.Issues
                    .Where(i => i.ProjectId == projectId)
                    .MaxAsync(i => (int?)i.Number, ct) ?? 0) + 1;

                // ── Issues ────────────────────────────────────────────────────────
                foreach (var ghIssue in ghIssues)
                {
                    var existing = await db.Issues
                        .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.GitHubIssueNumber == ghIssue.Number, ct);

                    if (existing is null)
                    {
                        await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                            $"Imported: {repo}#{ghIssue.Number} \"{TruncateTitle(ghIssue.Title)}\"", ct);

                        if (!dryRun)
                        {
                            db.Issues.Add(new Issue
                            {
                                Id = Guid.NewGuid(),
                                ProjectId = projectId,
                                Number = nextNumber,
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
                            nextNumber++;
                            await db.SaveChangesAsync(ct);
                        }

                        imported++;
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
                            if (!dryRun)
                            {
                                var pushResult = await PushIssueToGitHubAsync(existing, token, repo, ct);
                                if (pushResult)
                                {
                                    pushed++;
                                    await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                                        $"Pushed local changes: {repo}#{ghIssue.Number} \"{TruncateTitle(existing.Title)}\"", ct);
                                }
                                else
                                {
                                    await AppendLogAsync(run, GitHubSyncLogLevel.Warn,
                                        $"Failed to push: {repo}#{ghIssue.Number} \"{TruncateTitle(existing.Title)}\"", ct);
                                }
                            }
                            else
                            {
                                pushed++;
                                await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                                    $"[DRY RUN] Would push local changes: {repo}#{ghIssue.Number} \"{TruncateTitle(existing.Title)}\"", ct);
                            }
                        }
                        else
                        {
                            bool changed = false;
                            if (!string.Equals(existing.Title, ghIssue.Title, StringComparison.Ordinal))
                            {
                                if (!dryRun) existing.Title = ghIssue.Title;
                                changed = true;
                            }
                            if (!string.Equals(existing.Body ?? string.Empty, ghIssue.Body ?? string.Empty, StringComparison.Ordinal))
                            {
                                if (!dryRun) existing.Body = ghIssue.Body;
                                changed = true;
                            }
                            if (!string.Equals(existing.GitHubIssueUrl, ghIssue.HtmlUrl, StringComparison.Ordinal))
                            {
                                if (!dryRun) existing.GitHubIssueUrl = ghIssue.HtmlUrl;
                                changed = true;
                            }
                            if (changed)
                            {
                                if (!dryRun) existing.UpdatedAt = DateTime.UtcNow;
                                updated++;
                                await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                                    $"{(dryRun ? "[DRY RUN] Would update" : "Updated")} from GitHub: {repo}#{ghIssue.Number} \"{TruncateTitle(ghIssue.Title)}\"", ct);
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
                            if (!dryRun)
                            {
                                existing.GitHubIssueUrl = ghIssue.HtmlUrl;
                                existing.UpdatedAt = DateTime.UtcNow;
                            }
                            updated++;
                            await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                                $"{(dryRun ? "[DRY RUN] Would update" : "Updated")} link: {repo}#{ghIssue.Number} \"{TruncateTitle(ghIssue.Title)}\"", ct);
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                }

                // ── Pull Requests → MergeRequests ─────────────────────────────────
                foreach (var ghPr in ghPrs)
                {
                    var existingMr = await db.MergeRequests
                        .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.GitHubPrNumber == ghPr.Number, ct);

                    if (existingMr is null)
                    {
                        await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                            $"Imported PR: {repo}#{ghPr.Number} \"{TruncateTitle(ghPr.Title)}\"", ct);

                        if (!dryRun)
                        {
                            db.MergeRequests.Add(new MergeRequest
                            {
                                Id = Guid.NewGuid(),
                                ProjectId = projectId,
                                Title = ghPr.Title,
                                Description = ghPr.Body,
                                SourceBranch = ghPr.Head?.Ref ?? "unknown",
                                TargetBranch = ghPr.Base?.Ref ?? "main",
                                Status = MapGitHubPrState(ghPr.State, ghPr.MergedAt),
                                AutoMergeEnabled = false,
                                LastKnownSourceSha = ghPr.Head?.Sha,
                                GitHubPrNumber = ghPr.Number,
                                GitHubPrUrl = ghPr.HtmlUrl,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                            });
                            await db.SaveChangesAsync(ct);
                        }

                        prsImported++;
                    }
                    else
                    {
                        prsSkipped++;
                    }
                }

                if (!dryRun)
                    await db.SaveChangesAsync(ct);
            } // end if (syncContent.HasFlag(Issues))

            // ── GitHub Actions Workflow Runs → CiCdRuns ───────────────────────
            int cicdImported = 0, cicdUpdated = 0, cicdSkipped = 0;
            if (syncContent.HasFlag(GitHubSyncContent.CiCdBuilds))
            {
                List<GitHubWorkflowRunDto> ghRuns;
                try
                {
                    ghRuns = await FetchAllGitHubWorkflowRunsAsync(token, repo, run, ct);
                }
                catch (GitHubApiException ex)
                {
                    await AppendLogAsync(run, GitHubSyncLogLevel.Warn,
                        $"Failed to fetch GitHub Actions runs: {ex.Message}", ct);
                    ghRuns = [];
                }

                await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                    $"Fetched {ghRuns.Count} GitHub Actions workflow run(s).", ct);

                foreach (var ghRun in ghRuns)
                {
                    var externalRunId = ghRun.Id.ToString();
                    var existing = await db.CiCdRuns
                        .FirstOrDefaultAsync(r => r.ProjectId == projectId
                            && r.ExternalSource == "github"
                            && r.ExternalRunId == externalRunId, ct);

                    var status = MapGitHubActionsStatus(ghRun.Status, ghRun.Conclusion);

                    if (existing is null)
                    {
                        await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                            $"{(dryRun ? "[DRY RUN] Would import" : "Imported")} GitHub Actions run: {ghRun.Name} #{ghRun.Id} ({ghRun.Status}/{ghRun.Conclusion ?? "—"}) on {ghRun.HeadSha[..Math.Min(7, ghRun.HeadSha.Length)]}", ct);

                        if (!dryRun)
                        {
                            db.CiCdRuns.Add(new CiCdRun
                            {
                                Id = Guid.NewGuid(),
                                ProjectId = projectId,
                                CommitSha = ghRun.HeadSha,
                                Branch = ghRun.HeadBranch,
                                Workflow = ghRun.Name,
                                Status = status,
                                ExternalSource = "github",
                                ExternalRunId = externalRunId,
                                EventName = ghRun.Event,
                                StartedAt = ghRun.RunStartedAt ?? ghRun.CreatedAt,
                                EndedAt = ghRun.UpdatedAt,
                            });
                            await db.SaveChangesAsync(ct);
                        }

                        cicdImported++;
                    }
                    else
                    {
                        // Update status/SHA if changed.
                        bool changed = false;
                        if (existing.Status != status)
                        {
                            if (!dryRun) existing.Status = status;
                            changed = true;
                        }
                        if (!string.Equals(existing.CommitSha, ghRun.HeadSha, StringComparison.Ordinal))
                        {
                            if (!dryRun) existing.CommitSha = ghRun.HeadSha;
                            changed = true;
                        }
                        if (ghRun.UpdatedAt.HasValue && existing.EndedAt != ghRun.UpdatedAt)
                        {
                            if (!dryRun) existing.EndedAt = ghRun.UpdatedAt;
                            changed = true;
                        }

                        if (changed)
                        {
                            await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                                $"{(dryRun ? "[DRY RUN] Would update" : "Updated")} GitHub Actions run: {ghRun.Name} #{ghRun.Id}", ct);
                            cicdUpdated++;
                        }
                        else
                        {
                            cicdSkipped++;
                        }
                    }
                }

                if (!dryRun)
                    await db.SaveChangesAsync(ct);
            } // end if (syncContent.HasFlag(CiCdBuilds))

            // ── Summary ───────────────────────────────────────────────────────
            var parts = new List<string>();
            if (syncContent.HasFlag(GitHubSyncContent.Issues))
            {
                parts.Add(syncMode == GitHubSyncMode.TwoWay
                    ? $"{imported} imported, {updated} updated from GitHub, {pushed} pushed to GitHub, {skipped} unchanged; {prsImported} PRs imported, {prsSkipped} PRs unchanged"
                    : $"{imported} imported, {updated} updated, {skipped} unchanged; {prsImported} PRs imported, {prsSkipped} PRs unchanged");
            }
            if (syncContent.HasFlag(GitHubSyncContent.CiCdBuilds))
            {
                parts.Add($"{cicdImported} CI/CD runs imported, {cicdUpdated} updated, {cicdSkipped} unchanged");
            }

            run.Summary = string.Join("; ", parts);
            if (string.IsNullOrEmpty(run.Summary)) run.Summary = "Nothing to sync.";

            if (dryRun)
                run.Summary = "[DRY RUN] " + run.Summary;

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

        return (token, NormalizeRepo(config.GitHubRepo));
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
                throw new GitHubApiException(
                    $"GitHub API returned {(int)response.StatusCode} for issues page {page}. " +
                    "Check that the repository exists and the token has the required permissions.");
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
        string token, string repo, GitHubSyncRun? run, CancellationToken ct)
    {
        var (owner, repoName) = ParseRepo(repo);
        var client = CreateHttpClient(token);

        var allPrs = new List<GitHubPullRequestDto>();
        var page = 1;

        while (true)
        {
            var url = $"https://api.github.com/repos/{owner}/{repoName}/pulls?state=all&per_page=100&page={page}";
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new GitHubApiException(
                    $"GitHub API returned {(int)response.StatusCode} for pull requests page {page}. " +
                    "Check that the repository exists and the token has the required permissions.");
            }

            var prs = await response.Content.ReadFromJsonAsync<List<GitHubPullRequestDto>>(ct) ?? [];
            allPrs.AddRange(prs);

            if (prs.Count < 100)
                break;

            page++;
        }

        return allPrs;
    }

    private async Task<List<GitHubWorkflowRunDto>> FetchAllGitHubWorkflowRunsAsync(
        string token, string repo, GitHubSyncRun? run, CancellationToken ct)
    {
        var (owner, repoName) = ParseRepo(repo);
        var client = CreateHttpClient(token);

        var allRuns = new List<GitHubWorkflowRunDto>();
        var page = 1;

        while (true)
        {
            var url = $"https://api.github.com/repos/{owner}/{repoName}/actions/runs?per_page=100&page={page}";
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new GitHubApiException(
                    $"GitHub API returned {(int)response.StatusCode} for workflow runs page {page}. " +
                    "Check that the repository exists and the token has the 'actions:read' permission.");
            }

            var body = await response.Content.ReadFromJsonAsync<GitHubWorkflowRunsPageDto>(ct);
            var runs = body?.WorkflowRuns ?? [];
            allRuns.AddRange(runs);

            if (runs.Count < 100)
                break;

            page++;
        }

        return allRuns;
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
        var normalized = NormalizeRepo(ownerRepo);
        var parts = normalized.Split('/', 2);
        if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
            throw new ArgumentException($"Invalid GitHub repository format: \"{ownerRepo}\". Expected owner/repo.", nameof(ownerRepo));
        return (parts[0], parts[1]);
    }

    /// <summary>
    /// Normalizes various GitHub repository input formats to <c>owner/repo</c>.
    /// Accepts:
    ///   <c>https://github.com/owner/repo</c>,
    ///   <c>https://github.com/owner/repo.git</c>,
    ///   <c>git@github.com:owner/repo.git</c>,
    ///   <c>owner/repo</c>.
    /// </summary>
    internal static string NormalizeRepo(string raw)
    {
        var s = raw.Trim().TrimEnd('/');

        // https://github.com/owner/repo[.git]
        if (s.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            s = s["https://github.com/".Length..];

        // http://github.com/owner/repo[.git]
        else if (s.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
            s = s["http://github.com/".Length..];

        // git@github.com:owner/repo[.git]
        else if (s.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            s = s["git@github.com:".Length..];

        // strip trailing .git
        if (s.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            s = s[..^".git".Length];

        return s;
    }

    private static MergeRequestStatus MapGitHubPrState(string? state, string? mergedAt) =>
        mergedAt is not null ? MergeRequestStatus.Merged :
        state?.ToLowerInvariant() == "closed" ? MergeRequestStatus.Closed : MergeRequestStatus.Open;

    private static IssueStatus MapGitHubState(string? state) =>
        state?.ToLowerInvariant() == "closed" ? IssueStatus.Done : IssueStatus.Backlog;

    private static string TruncateTitle(string title, int max = 60) =>
        title.Length <= max ? title : title[..max] + "...";

    /// <summary>
    /// Maps a GitHub Actions workflow run status/conclusion pair to a <see cref="CiCdRunStatus"/>.
    /// GitHub status values: <c>queued</c>, <c>in_progress</c>, <c>completed</c>, <c>waiting</c>,
    /// <c>requested</c>, <c>pending</c>. GitHub conclusion values (when status=completed):
    /// <c>success</c>, <c>failure</c>, <c>cancelled</c>, <c>timed_out</c>, <c>action_required</c>,
    /// <c>neutral</c>, <c>skipped</c>, <c>stale</c>, <c>startup_failure</c>.
    /// </summary>
    private static CiCdRunStatus MapGitHubActionsStatus(string? status, string? conclusion)
    {
        return status?.ToLowerInvariant() switch
        {
            "queued" or "waiting" or "requested" or "pending" => CiCdRunStatus.Pending,
            "in_progress" => CiCdRunStatus.Running,
            "completed" => conclusion?.ToLowerInvariant() switch
            {
                "success" => CiCdRunStatus.Succeeded,
                "cancelled" => CiCdRunStatus.Cancelled,
                "failure" or "timed_out" or "startup_failure" => CiCdRunStatus.Failed,
                _ => CiCdRunStatus.Succeeded, // neutral/skipped/stale/action_required → treat as succeeded
            },
            _ => CiCdRunStatus.Pending,
        };
    }

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
        public string? MergedAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("head")]
        public GitHubBranchRef? Head { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("base")]
        public GitHubBranchRef? Base { get; set; }
    }

    private sealed class GitHubBranchRef
    {
        [System.Text.Json.Serialization.JsonPropertyName("ref")]
        public string Ref { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("sha")]
        public string? Sha { get; set; }
    }

    private sealed class GitHubWorkflowRunsPageDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("workflow_runs")]
        public List<GitHubWorkflowRunDto> WorkflowRuns { get; set; } = [];
    }

    private sealed class GitHubWorkflowRunDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public long Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("head_sha")]
        public string HeadSha { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("head_branch")]
        public string? HeadBranch { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("event")]
        public string? Event { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string? Status { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("conclusion")]
        public string? Conclusion { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("run_started_at")]
        public DateTime? RunStartedAt { get; set; }
    }
}

/// <summary>Thrown when the GitHub API returns a non-success response during a sync fetch.</summary>
public sealed class GitHubApiException(string message) : Exception(message);

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
