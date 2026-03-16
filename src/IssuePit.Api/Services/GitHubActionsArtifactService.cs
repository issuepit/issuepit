using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using IssuePit.Core.Data;
using IssuePit.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Downloads GitHub Actions run artifacts and extracts/parses any <c>.trx</c> test-result files
/// contained within them, storing the results as <see cref="IssuePit.Core.Entities.CiCdTestSuite"/>
/// rows linked to the matching <see cref="IssuePit.Core.Entities.CiCdRun"/>.
/// </summary>
public class GitHubActionsArtifactService(
    IssuePitDbContext db,
    IDataProtectionProvider dpProvider,
    IHttpClientFactory httpClientFactory,
    ILogger<GitHubActionsArtifactService> logger)
{
    private const string ProtectorPurpose = "GitHubOAuthToken";

    /// <summary>
    /// Resolves the GitHub token and repo for <paramref name="projectId"/>, lists artifacts
    /// for the GitHub Actions run identified by <paramref name="externalRunId"/>, downloads
    /// each artifact ZIP, extracts <c>.trx</c> files, and stores the parsed test results.
    /// Best-effort: errors are logged but never propagated.
    /// Returns immediately (no-op) when the project has no GitHub sync configuration.
    /// </summary>
    public async Task ProcessArtifactsAsync(
        Guid runId,
        Guid projectId,
        string externalRunId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (token, repo) = await ResolveTokenAndRepoAsync(projectId, cancellationToken);
            if (token is null || repo is null)
            {
                logger.LogDebug(
                    "Skipping GitHub artifact processing for run {RunId}: no GitHub sync config for project {ProjectId}",
                    runId, projectId);
                return;
            }

            var (owner, repoName) = ParseRepo(repo);
            var client = CreateHttpClient(token);

            // List artifacts for the GitHub Actions run.
            var artifactItems = await ListArtifactsAsync(client, owner, repoName, externalRunId, cancellationToken);
            if (artifactItems.Count == 0)
            {
                logger.LogDebug("No artifacts found for GitHub run {ExternalRunId} (project {ProjectId})", externalRunId, projectId);
                return;
            }

            var suiteCount = 0;

            foreach (var artifact in artifactItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Download the artifact ZIP.
                byte[]? zipBytes = null;
                try
                {
                    zipBytes = await DownloadArtifactAsync(client, owner, repoName, artifact.Id.ToString(), cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex,
                        "Failed to download artifact '{ArtifactName}' (id={ArtifactId}) for run {RunId}",
                        artifact.Name, artifact.Id, runId);
                    continue;
                }

                if (zipBytes is null || zipBytes.Length == 0)
                    continue;

                // Extract and parse any .trx files from the ZIP.
                suiteCount += ParseTrxFromZip(zipBytes, artifact.Name, runId, cancellationToken);
            }

            if (suiteCount > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Stored {Count} TRX test suite(s) from GitHub Actions artifacts for run {RunId}",
                    suiteCount, runId);
            }
            else
            {
                logger.LogDebug("No TRX files found in GitHub Actions artifacts for run {RunId}", runId);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process GitHub Actions artifacts for run {RunId}", runId);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<List<GitHubArtifactItem>> ListArtifactsAsync(
        HttpClient client, string owner, string repo, string runId, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/actions/runs/{runId}/artifacts?per_page=100";
        var response = await client.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "GitHub API returned {StatusCode} when listing artifacts for run {RunId} ({Owner}/{Repo})",
                (int)response.StatusCode, runId, owner, repo);
            return [];
        }

        var result = await response.Content.ReadFromJsonAsync<GitHubArtifactsResponse>(ct);
        return result?.Artifacts ?? [];
    }

    private async Task<byte[]?> DownloadArtifactAsync(
        HttpClient client, string owner, string repo, string artifactId, CancellationToken ct)
    {
        // GitHub redirects artifact download requests; allow redirects (default HttpClient behaviour).
        var url = $"https://api.github.com/repos/{owner}/{repo}/actions/artifacts/{artifactId}/zip";
        var response = await client.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "GitHub API returned {StatusCode} when downloading artifact {ArtifactId} ({Owner}/{Repo})",
                (int)response.StatusCode, artifactId, owner, repo);
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private int ParseTrxFromZip(byte[] zipBytes, string artifactName, Guid runId,
        CancellationToken cancellationToken)
    {
        var suiteCount = 0;
        try
        {
            using var ms = new MemoryStream(zipBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!entry.FullName.EndsWith(".trx", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    using var stream = entry.Open();
                    var suite = TrxParser.Parse(stream, artifactName);
                    if (suite is null)
                    {
                        logger.LogDebug(
                            "Could not parse TRX entry '{Entry}' in artifact '{Artifact}' for run {RunId}",
                            entry.FullName, artifactName, runId);
                        continue;
                    }

                    suite.CiCdRunId = runId;
                    db.CiCdTestSuites.Add(suite);
                    suiteCount++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogDebug(ex,
                        "Error reading TRX entry '{Entry}' in artifact '{Artifact}' for run {RunId}",
                        entry.FullName, artifactName, runId);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Failed to open ZIP for artifact '{ArtifactName}' for run {RunId}", artifactName, runId);
        }

        return suiteCount;
    }

    private async Task<(string? token, string? repo)> ResolveTokenAndRepoAsync(
        Guid projectId, CancellationToken ct)
    {
        var config = await db.GitHubSyncConfigs
            .Include(c => c.GitHubIdentity)
            .FirstOrDefaultAsync(c => c.ProjectId == projectId, ct);

        if (config?.GitHubIdentity is null || string.IsNullOrWhiteSpace(config.GitHubRepo))
            return (null, null);

        if (!config.GitHubRepo.Contains('/'))
            return (null, null);

        var token = DecryptToken(config.GitHubIdentity.EncryptedToken);
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
        return (parts[0], parts[1]);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DTO types
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class GitHubArtifactsResponse
    {
        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }

        [JsonPropertyName("artifacts")]
        public List<GitHubArtifactItem> Artifacts { get; set; } = [];
    }

    private sealed class GitHubArtifactItem
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("archive_download_url")]
        public string ArchiveDownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("expired")]
        public bool Expired { get; set; }
    }
}
