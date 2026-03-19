using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Detects similar issues for a project using a two-phase approach:
/// 1. Fast pre-filter using keyword overlap scoring
/// 2. LLM re-ranking via OpenRouter to score and explain the top candidates
/// </summary>
public class SimilarIssueService(
    IssuePitDbContext db,
    ApiKeyResolverService keyResolver,
    IHttpClientFactory httpClientFactory,
    ILogger<SimilarIssueService> logger)
{
    private const string OpenRouterBaseUrl = "https://openrouter.ai/api/v1";
    private const string DefaultModel = "anthropic/claude-3.5-sonnet";
    private const int MaxCandidates = 30;
    private const int MaxResults = 5;
    private const int TruncateBodyAt = 1500;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Runs the full similar-issues detection for all issues in the given project.</summary>
    public async Task<SimilarIssueRun> DetectAsync(Guid projectId, CancellationToken ct = default)
    {
        var run = new SimilarIssueRun { Id = Guid.NewGuid(), ProjectId = projectId };
        db.SimilarIssueRuns.Add(run);
        await db.SaveChangesAsync(ct);

        try
        {
            run.Status = GitHubSyncRunStatus.Running;
            await db.SaveChangesAsync(ct);

            await AppendLogAsync(run, GitHubSyncLogLevel.Info, $"Starting similar issue detection for project {projectId}.", ct);

            var project = await db.Projects
                .Include(p => p.Organization)
                .FirstOrDefaultAsync(p => p.Id == projectId, ct)
                ?? throw new InvalidOperationException($"Project {projectId} not found.");

            var apiKey = await keyResolver.ResolveAsync(
                project.OrgId, ApiKeyProvider.OpenRouter, projectId: projectId, ct: ct);

            if (apiKey is null)
            {
                await AppendLogAsync(run, GitHubSyncLogLevel.Warn,
                    "No OpenRouter API key configured. Skipping LLM re-ranking; using keyword similarity only.", ct);
            }

            var plainKey = apiKey is not null ? ApiKeyResolverService.DecryptValue(apiKey.EncryptedValue) : null;

            // Fetch all issues for the project
            var issues = await db.Issues
                .Where(i => i.ProjectId == projectId)
                .Select(i => new { i.Id, i.Number, i.Title, i.Body, i.Status })
                .ToListAsync(ct);

            await AppendLogAsync(run, GitHubSyncLogLevel.Info, $"Loaded {issues.Count} issues.", ct);

            int pairsUpserted = 0;

            foreach (var issue in issues)
            {
                ct.ThrowIfCancellationRequested();

                // Phase 1: keyword pre-filter
                var searchText = BuildSearchText(issue.Title, issue.Body);
                var candidates = await GetCandidatesAsync(projectId, issue.Id, searchText, ct);

                if (candidates.Count == 0) continue;

                await AppendLogAsync(run, GitHubSyncLogLevel.Info,
                    $"Issue #{issue.Number}: {candidates.Count} candidate(s) found.", ct);

                List<SimilarIssueResult> results;

                if (plainKey is not null)
                {
                    // Phase 2: LLM re-rank
                    results = await LlmRerankAsync(issue.Id, issue.Title, issue.Body, candidates, plainKey, ct);
                }
                else
                {
                    // Fallback: use keyword scores directly
                    results = candidates
                        .Take(MaxResults)
                        .Select(c => new SimilarIssueResult(c.Id, c.Score, null))
                        .ToList();
                }

                // Upsert pairs
                foreach (var result in results.Where(r => r.Score > 0.1f).Take(MaxResults))
                {
                    var existing = await db.SimilarIssuePairs
                        .FirstOrDefaultAsync(p => p.IssueId == issue.Id && p.SimilarIssueId == result.SimilarIssueId, ct);

                    if (existing is not null)
                    {
                        existing.Score = result.Score;
                        existing.Reason = result.Reason;
                        existing.DetectedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        db.SimilarIssuePairs.Add(new SimilarIssuePair
                        {
                            Id = Guid.NewGuid(),
                            IssueId = issue.Id,
                            SimilarIssueId = result.SimilarIssueId,
                            Score = result.Score,
                            Reason = result.Reason,
                            DetectedAt = DateTime.UtcNow,
                        });
                    }
                    pairsUpserted++;
                }

                await db.SaveChangesAsync(ct);
            }

            run.Status = GitHubSyncRunStatus.Succeeded;
            run.CompletedAt = DateTime.UtcNow;
            run.Summary = $"Processed {issues.Count} issue(s), {pairsUpserted} similar pair(s) found.";
            await AppendLogAsync(run, GitHubSyncLogLevel.Info, run.Summary, ct);
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            run.Status = GitHubSyncRunStatus.Failed;
            run.CompletedAt = DateTime.UtcNow;
            run.Summary = "Run was cancelled.";
            await db.SaveChangesAsync(default);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SimilarIssueService failed for project {ProjectId}.", projectId);
            run.Status = GitHubSyncRunStatus.Failed;
            run.CompletedAt = DateTime.UtcNow;
            run.Summary = $"Failed: {ex.Message}";
            await db.SaveChangesAsync(default);
        }

        return run;
    }

    private sealed record CandidateIssue(Guid Id, int Number, string Title, string? Body, float Score);
    private sealed record SimilarIssueResult(Guid SimilarIssueId, float Score, string? Reason);

    private async Task<List<CandidateIssue>> GetCandidatesAsync(
        Guid projectId, Guid excludeId, string searchText, CancellationToken ct)
    {
        var lowerSearch = searchText.ToLower();
        var words = lowerSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Distinct()
            .Take(10)
            .ToList();

        if (words.Count == 0) return [];

        // Get all candidates from the project (excluding the source issue)
        var allIssues = await db.Issues
            .Where(i => i.ProjectId == projectId && i.Id != excludeId)
            .Select(i => new { i.Id, i.Number, i.Title, i.Body })
            .ToListAsync(ct);

        // Score each candidate by keyword overlap
        var scored = allIssues
            .Select(i =>
            {
                var text = $"{i.Title} {i.Body ?? ""}".ToLower();
                var matchCount = words.Count(w => text.Contains(w));
                var score = words.Count > 0 ? (float)matchCount / words.Count : 0f;
                return new CandidateIssue(i.Id, i.Number, i.Title, i.Body, score);
            })
            .Where(c => c.Score > 0)
            .OrderByDescending(c => c.Score)
            .Take(MaxCandidates)
            .ToList();

        return scored;
    }

    private async Task<List<SimilarIssueResult>> LlmRerankAsync(
        Guid sourceIssueId,
        string sourceTitle,
        string? sourceBody,
        List<CandidateIssue> candidates,
        string apiKey,
        CancellationToken ct)
    {
        var candidatesText = new StringBuilder();
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            var body = c.Body is not null
                ? (c.Body.Length > TruncateBodyAt ? c.Body[..TruncateBodyAt] + "..." : c.Body)
                : "";
            candidatesText.AppendLine($"[{i + 1}] ID: {c.Id} | #{c.Number}: {c.Title}");
            if (!string.IsNullOrWhiteSpace(body))
                candidatesText.AppendLine($"    Body: {body}");
        }

        var sourceBodyText = sourceBody is not null
            ? (sourceBody.Length > TruncateBodyAt ? sourceBody[..TruncateBodyAt] + "..." : sourceBody)
            : "(no description)";

        var prompt = $"""
            You are analyzing software issue similarity.

            Source issue:
            Title: {sourceTitle}
            Body: {sourceBodyText}

            Candidate issues to evaluate:
            {candidatesText}

            Return a JSON array of the top {MaxResults} most similar candidates (only those with score > 0.1), ordered by similarity score descending.
            Each item must have:
            - "id": the candidate issue UUID
            - "score": float between 0.0 and 1.0
            - "reason": one sentence explaining why they are similar

            Return ONLY valid JSON array, no markdown, no explanation.
            Example: array of objects with id, score, reason fields.
            """;

        var requestBody = new
        {
            model = DefaultModel,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{OpenRouterBaseUrl}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var client = httpClientFactory.CreateClient("openrouter");
        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await client.SendAsync(request, ct);
            httpResponse.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM re-rank request failed for issue {IssueId}.", sourceIssueId);
            // Fallback to keyword scores
            return candidates.Take(MaxResults)
                .Select(c => new SimilarIssueResult(c.Id, c.Score, null))
                .ToList();
        }

        var responseJson = await httpResponse.Content.ReadAsStringAsync(ct);
        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(responseJson, JsonOptions);
            var content = parsed
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "[]";

            // Strip markdown code fences if present
            content = content.Trim();
            if (content.StartsWith("```")) content = content[(content.IndexOf('\n') + 1)..];
            if (content.EndsWith("```")) content = content[..content.LastIndexOf("```")].TrimEnd();

            var llmResults = JsonSerializer.Deserialize<List<JsonElement>>(content, JsonOptions) ?? [];

            // Validate IDs against the candidate set
            var candidateIds = candidates.Select(c => c.Id).ToHashSet();
            var results = new List<SimilarIssueResult>();
            foreach (var item in llmResults)
            {
                if (!item.TryGetProperty("id", out var idEl)) continue;
                if (!Guid.TryParse(idEl.GetString(), out var id)) continue;
                if (!candidateIds.Contains(id)) continue;

                var score = item.TryGetProperty("score", out var scoreEl) ? scoreEl.GetSingle() : 0.5f;
                var reason = item.TryGetProperty("reason", out var reasonEl) ? reasonEl.GetString() : null;
                results.Add(new SimilarIssueResult(id, Math.Clamp(score, 0f, 1f), reason));
            }

            return results.Take(MaxResults).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse LLM re-rank response for issue {IssueId}.", sourceIssueId);
            return candidates.Take(MaxResults)
                .Select(c => new SimilarIssueResult(c.Id, c.Score, null))
                .ToList();
        }
    }

    private static string BuildSearchText(string title, string? body)
    {
        var sb = new StringBuilder(title);
        if (!string.IsNullOrWhiteSpace(body))
        {
            sb.Append(' ');
            sb.Append(body.Length > 500 ? body[..500] : body);
        }
        return sb.ToString();
    }

    private async Task AppendLogAsync(SimilarIssueRun run, GitHubSyncLogLevel level, string message, CancellationToken ct)
    {
        logger.LogInformation("[SimilarIssue] [{Level}] {Message}", level, message);
        db.SimilarIssueRunLogs.Add(new SimilarIssueRunLog
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Level = level,
            Message = message,
            Timestamp = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}
