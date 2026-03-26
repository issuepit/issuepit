using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Handles Jira → IssuePit issue synchronisation (import only; Jira is read-only).
/// </summary>
public class JiraSyncService(
    IssuePitDbContext db,
    IDataProtectionProvider dpProvider,
    IHttpClientFactory httpClientFactory,
    ILogger<JiraSyncService> logger)
{
    private const string ProtectorPurpose = "ApiKeyValue";

    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs a Jira import for the given project. Creates a <see cref="JiraSyncRun"/> audit record.
    /// </summary>
    public async Task<JiraSyncRun> SyncAsync(Guid projectId, bool dryRun = false, CancellationToken ct = default)
    {
        var run = await CreateRunAsync(projectId, ct);

        try
        {
            await SetRunStatusAsync(run, GitHubSyncRunStatus.Running, ct);

            if (dryRun)
                await AppendLogAsync(run, GitHubSyncLogLevel.Info, "DRY RUN mode — no changes will be saved.", ct);

            var config = await db.JiraSyncConfigs
                .Include(c => c.ApiKey)
                .FirstOrDefaultAsync(c => c.ProjectId == projectId, ct);

            var resolved = await ResolveCredentialsAsync(projectId, run, ct);
            if (resolved is null)
            {
                await FailRunAsync(run, "Jira sync configuration incomplete — set the Jira base URL, project key, email, and API token.", ct);
                return run;
            }

            var baseUrl = resolved.Value.baseUrl;
            var projectKey = resolved.Value.projectKey;
            var email = resolved.Value.email;
            var apiToken = resolved.Value.apiToken;

            await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                $"Starting Jira import for project {projectKey} from {baseUrl}...", ct);

            // Require the project to have a short key configured.
            var projectIssueKey = await db.Projects
                .Where(p => p.Id == projectId)
                .Select(p => p.IssueKey)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(projectIssueKey))
            {
                await FailRunAsync(run,
                    "Jira import requires a project key (short slug) to be configured. " +
                    "Set a Project Key in Project Settings → Issue ID Format first, then re-run the sync.", ct);
                return run;
            }

            // Ensure a single IssueExternalSource record exists for this project + Jira project.
            var jiraProjectUrl = $"{baseUrl.TrimEnd('/')}/jira/software/projects/{projectKey}";
            var externalSource = await db.IssueExternalSources
                .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.Type == "jira", ct);
            if (externalSource is null)
            {
                externalSource = new IssueExternalSource
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Type = "jira",
                    Slug = projectKey,
                    Url = jiraProjectUrl,
                };
                db.IssueExternalSources.Add(externalSource);
                await db.SaveChangesAsync(ct);
            }
            else if (externalSource.Url != jiraProjectUrl || externalSource.Slug != projectKey)
            {
                externalSource.Url = jiraProjectUrl;
                externalSource.Slug = projectKey;
                await db.SaveChangesAsync(ct);
            }

            bool onlyWithParent = config?.OnlyImportWithParent ?? false;
            bool importComments = config?.ImportComments ?? true;

            List<JiraIssueDto> jiraIssues;
            try
            {
                jiraIssues = await FetchAllJiraIssuesAsync(baseUrl, projectKey, email, apiToken, onlyWithParent, run, ct);
            }
            catch (JiraApiException ex)
            {
                await FailRunAsync(run, ex.Message, ct);
                return run;
            }

            await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                $"Fetched {jiraIssues.Count} issue(s) from Jira project {projectKey}.", ct);

            // Import oldest first.
            jiraIssues = [.. jiraIssues.OrderBy(i => i.Key, StringComparer.OrdinalIgnoreCase)];

            var nextNumber = (await db.Issues
                .Where(i => i.ProjectId == projectId)
                .MaxAsync(i => (int?)i.Number, ct) ?? 0) + 1;

            int imported = 0, skipped = 0;

            foreach (var jiraIssue in jiraIssues)
            {
                // Check if already imported by external ID (Jira issue number).
                var existing = await db.Issues
                    .FirstOrDefaultAsync(i => i.ProjectId == projectId
                        && i.ExternalSourceId == externalSource.Id
                        && i.ExternalId == jiraIssue.IssueNumber, ct);

                if (existing is not null)
                {
                    skipped++;
                    continue;
                }

                var title = jiraIssue.Fields?.Summary ?? jiraIssue.Key;
                var body = jiraIssue.Fields?.Description?.ContentAsText();
                var status = MapJiraStatus(jiraIssue.Fields?.Status?.StatusCategory?.Key);

                await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                    $"Imported: {jiraIssue.Key} \"{TruncateTitle(title)}\"", ct);

                if (!dryRun)
                {
                    var issue = new Issue
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        Number = nextNumber,
                        Title = title,
                        Body = body,
                        Status = status,
                        Priority = MapJiraPriority(jiraIssue.Fields?.Priority?.Name),
                        Type = IssueType.Issue,
                        ExternalId = jiraIssue.IssueNumber,
                        ExternalSourceId = externalSource.Id,
                        CreatedAt = jiraIssue.Fields?.Created ?? DateTime.UtcNow,
                        UpdatedAt = jiraIssue.Fields?.Updated ?? DateTime.UtcNow,
                    };
                    db.Issues.Add(issue);
                    await db.SaveChangesAsync(ct);

                    // Import comments if enabled.
                    if (importComments)
                    {
                        try
                        {
                            var comments = await FetchJiraCommentsAsync(baseUrl, jiraIssue.Key, email, apiToken, ct);
                            foreach (var comment in comments)
                            {
                                var commentBody = comment.Body?.ContentAsText() ?? string.Empty;
                                if (string.IsNullOrWhiteSpace(commentBody)) continue;
                                db.IssueComments.Add(new IssueComment
                                {
                                    Id = Guid.NewGuid(),
                                    IssueId = issue.Id,
                                    Body = commentBody,
                                    CreatedAt = comment.Created ?? DateTime.UtcNow,
                                    UpdatedAt = comment.Updated ?? DateTime.UtcNow,
                                    UserId = null,
                                });
                            }
                            await db.SaveChangesAsync(ct);
                        }
                        catch (Exception ex)
                        {
                            await AppendLogAsync(run, GitHubSyncLogLevel.Warn,
                                $"Could not import comments for {jiraIssue.Key}: {ex.Message}", ct);
                        }
                    }

                    nextNumber++;
                }

                imported++;
            }

            var summary = dryRun
                ? $"[DRY RUN] Would import {imported} issue(s); {skipped} already present."
                : $"{imported} imported, {skipped} skipped (already present).";
            await AppendLogAsync(run, GitHubSyncLogLevel.Info, summary, ct);
            run.Summary = summary;
            await CompleteRunAsync(run, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception during Jira sync for project {ProjectId}", projectId);
            await FailRunAsync(run, $"Unexpected error: {ex.Message}", ct);
        }

        return run;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<(string baseUrl, string projectKey, string email, string apiToken)?> ResolveCredentialsAsync(
        Guid projectId, JiraSyncRun? run, CancellationToken ct)
    {
        var config = await db.JiraSyncConfigs
            .Include(c => c.ApiKey)
            .FirstOrDefaultAsync(c => c.ProjectId == projectId, ct);

        if (config is null)
        {
            await AppendLogAsync(run, GitHubSyncLogLevel.Warn, "No Jira sync configuration found for this project.", ct);
            return null;
        }

        if (string.IsNullOrWhiteSpace(config.JiraBaseUrl))
        {
            await AppendLogAsync(run, GitHubSyncLogLevel.Warn, "Jira base URL is not configured.", ct);
            return null;
        }

        if (string.IsNullOrWhiteSpace(config.JiraProjectKey))
        {
            await AppendLogAsync(run, GitHubSyncLogLevel.Warn, "Jira project key is not configured.", ct);
            return null;
        }

        if (string.IsNullOrWhiteSpace(config.JiraEmail))
        {
            await AppendLogAsync(run, GitHubSyncLogLevel.Warn, "Jira user email is not configured.", ct);
            return null;
        }

        if (config.ApiKey is null)
        {
            await AppendLogAsync(run, GitHubSyncLogLevel.Warn, "No Jira API key linked to sync configuration.", ct);
            return null;
        }

        var apiToken = DecryptApiKeyValue(config.ApiKey.EncryptedValue);
        if (apiToken is null)
        {
            await AppendLogAsync(run, GitHubSyncLogLevel.Error, "Failed to decrypt Jira API token.", ct);
            return null;
        }

        return (config.JiraBaseUrl.TrimEnd('/'), config.JiraProjectKey.Trim(), config.JiraEmail.Trim(), apiToken);
    }

    private string? DecryptApiKeyValue(string encryptedValue)
    {
        try
        {
            // Support plain: prefix for development/test keys (same as ApiKey storage convention).
            if (encryptedValue.StartsWith("plain:", StringComparison.Ordinal))
                return encryptedValue["plain:".Length..];

            var protector = dpProvider.CreateProtector(ProtectorPurpose);
            return protector.Unprotect(encryptedValue);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decrypt Jira API token");
            return null;
        }
    }

    private async Task<List<JiraIssueDto>> FetchAllJiraIssuesAsync(
        string baseUrl, string projectKey, string email, string apiToken,
        bool onlyWithParent, JiraSyncRun? run, CancellationToken ct)
    {
        var client = CreateHttpClient(baseUrl, email, apiToken);
        var allIssues = new List<JiraIssueDto>();
        var startAt = 0;
        const int maxResults = 100;

        // Build JQL — optionally filter to only issues with a parent.
        var jql = onlyWithParent
            ? $"project = \"{projectKey}\" AND parent IS NOT EMPTY ORDER BY created ASC"
            : $"project = \"{projectKey}\" ORDER BY created ASC";

        while (true)
        {
            var url = $"{baseUrl}/rest/api/3/search?jql={Uri.EscapeDataString(jql)}&startAt={startAt}&maxResults={maxResults}&fields=summary,description,status,priority,created,updated,parent,issuetype";
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new JiraApiException(
                    $"Jira API returned {(int)response.StatusCode} when fetching issues. " +
                    "Verify your credentials and that the API token has Browse Projects permission. " +
                    $"Details: {TruncateTitle(body, 200)}");
            }

            var page = await response.Content.ReadFromJsonAsync<JiraSearchResultDto>(ct);
            if (page is null) break;

            allIssues.AddRange(page.Issues);

            if (allIssues.Count >= page.Total || page.Issues.Count < maxResults)
                break;

            startAt += page.Issues.Count;
        }

        return allIssues;
    }

    private async Task<List<JiraCommentDto>> FetchJiraCommentsAsync(
        string baseUrl, string issueKey, string email, string apiToken, CancellationToken ct)
    {
        var client = CreateHttpClient(baseUrl, email, apiToken);
        var allComments = new List<JiraCommentDto>();
        var startAt = 0;
        const int maxResults = 100;

        while (true)
        {
            var url = $"{baseUrl}/rest/api/3/issue/{Uri.EscapeDataString(issueKey)}/comment?startAt={startAt}&maxResults={maxResults}";
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                break; // Non-fatal — log at call site.

            var page = await response.Content.ReadFromJsonAsync<JiraCommentPageDto>(ct);
            if (page is null) break;

            allComments.AddRange(page.Comments);

            if (allComments.Count >= page.Total || page.Comments.Count < maxResults)
                break;

            startAt += page.Comments.Count;
        }

        return allComments;
    }

    private HttpClient CreateHttpClient(string baseUrl, string email, string apiToken)
    {
        var client = httpClientFactory.CreateClient();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    private static IssueStatus MapJiraStatus(string? statusCategoryKey) =>
        statusCategoryKey?.ToLowerInvariant() switch
        {
            "done" => IssueStatus.Done,
            "indeterminate" => IssueStatus.InProgress,
            _ => IssueStatus.Backlog,
        };

    private static IssuePriority MapJiraPriority(string? priorityName) =>
        priorityName?.ToLowerInvariant() switch
        {
            "highest" or "blocker" => IssuePriority.Urgent,
            "high" or "critical" => IssuePriority.High,
            "medium" or "normal" => IssuePriority.Medium,
            "low" or "minor" => IssuePriority.Low,
            _ => IssuePriority.NoPriority,
        };

    private static string TruncateTitle(string title, int max = 60) =>
        title.Length <= max ? title : title[..max] + "...";

    // ──────────────────────────────────────────────────────────────────────────
    // Run lifecycle helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<JiraSyncRun> CreateRunAsync(Guid projectId, CancellationToken ct)
    {
        var run = new JiraSyncRun
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Status = GitHubSyncRunStatus.Pending,
            StartedAt = DateTime.UtcNow,
        };
        db.JiraSyncRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    private async Task SetRunStatusAsync(JiraSyncRun run, GitHubSyncRunStatus status, CancellationToken ct)
    {
        run.Status = status;
        await db.SaveChangesAsync(ct);
    }

    private async Task CompleteRunAsync(JiraSyncRun run, CancellationToken ct)
    {
        run.Status = GitHubSyncRunStatus.Succeeded;
        run.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task FailRunAsync(JiraSyncRun run, string reason, CancellationToken ct)
    {
        run.Status = GitHubSyncRunStatus.Failed;
        run.CompletedAt = DateTime.UtcNow;
        run.Summary = reason;
        db.JiraSyncRunLogs.Add(new JiraSyncRunLog
        {
            Id = Guid.NewGuid(),
            SyncRunId = run.Id,
            Level = GitHubSyncLogLevel.Error,
            Message = reason,
            Timestamp = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task AppendLogAsync(JiraSyncRun? run, GitHubSyncLogLevel level, string message, CancellationToken ct)
    {
        if (run is null) return;
        db.JiraSyncRunLogs.Add(new JiraSyncRunLog
        {
            Id = Guid.NewGuid(),
            SyncRunId = run.Id,
            Level = level,
            Message = message,
            Timestamp = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DTO types — Jira Cloud REST API v3
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class JiraSearchResultDto
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("issues")]
        public List<JiraIssueDto> Issues { get; set; } = [];
    }

    private sealed class JiraIssueDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        /// <summary>Numeric Jira issue ID extracted from the <see cref="Id"/> field.</summary>
        public int IssueNumber => int.TryParse(Id, out var n) ? n : 0;

        [JsonPropertyName("fields")]
        public JiraIssueFieldsDto? Fields { get; set; }
    }

    private sealed class JiraIssueFieldsDto
    {
        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("description")]
        public JiraDocDto? Description { get; set; }

        [JsonPropertyName("status")]
        public JiraStatusDto? Status { get; set; }

        [JsonPropertyName("priority")]
        public JiraPriorityDto? Priority { get; set; }

        [JsonPropertyName("parent")]
        public JiraParentDto? Parent { get; set; }

        [JsonPropertyName("created")]
        public DateTime? Created { get; set; }

        [JsonPropertyName("updated")]
        public DateTime? Updated { get; set; }
    }

    private sealed class JiraStatusDto
    {
        [JsonPropertyName("statusCategory")]
        public JiraStatusCategoryDto? StatusCategory { get; set; }
    }

    private sealed class JiraStatusCategoryDto
    {
        [JsonPropertyName("key")]
        public string? Key { get; set; }
    }

    private sealed class JiraPriorityDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class JiraParentDto
    {
        [JsonPropertyName("key")]
        public string? Key { get; set; }
    }

    /// <summary>Jira Atlassian Document Format (ADF) node.</summary>
    private sealed class JiraDocDto
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("content")]
        public List<JiraDocDto>? Content { get; set; }

        /// <summary>Recursively extracts plain text from an ADF document tree.</summary>
        public string ContentAsText()
        {
            if (Type == "text") return Text ?? string.Empty;
            if (Content is null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            foreach (var node in Content)
            {
                var text = node.ContentAsText();
                if (!string.IsNullOrEmpty(text))
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(text);
                }
            }
            return sb.ToString();
        }
    }

    private sealed class JiraCommentPageDto
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("comments")]
        public List<JiraCommentDto> Comments { get; set; } = [];
    }

    private sealed class JiraCommentDto
    {
        [JsonPropertyName("body")]
        public JiraDocDto? Body { get; set; }

        [JsonPropertyName("created")]
        public DateTime? Created { get; set; }

        [JsonPropertyName("updated")]
        public DateTime? Updated { get; set; }
    }
}

/// <summary>Thrown when the Jira API returns a non-success response during a sync fetch.</summary>
public sealed class JiraApiException(string message) : Exception(message);
