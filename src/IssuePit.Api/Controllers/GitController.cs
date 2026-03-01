using System.Net.Http.Headers;
using System.Text.Json;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/git")]
public class GitController(
    IssuePitDbContext db,
    TenantContext ctx,
    IDataProtectionProvider dpProvider,
    IHttpClientFactory httpClientFactory,
    ILogger<GitController> logger) : ControllerBase
{
    private static readonly string ProtectorPurpose = "GitHubOAuthToken";
    private const string GitHubApiBase = "https://api.github.com";
    private const int BranchesPerPage = 100;
    private const int CommitsPerPage = 30;

    /// <summary>Returns the branches for the project's linked GitHub repository.</summary>
    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches(Guid projectId)
    {
        var (repo, client) = await GetGitHubClientAsync(projectId);
        if (repo is null) return NotFound("Project not found or has no GitHub repository configured.");

        var response = await client.GetAsync($"{GitHubApiBase}/repos/{repo}/branches?per_page={BranchesPerPage}");
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Failed to fetch branches from GitHub.");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var branches = doc.RootElement.EnumerateArray().Select(b => new
        {
            name = b.GetProperty("name").GetString(),
            sha = b.GetProperty("commit").GetProperty("sha").GetString(),
            isProtected = b.GetProperty("protected").GetBoolean(),
        }).ToList();

        return Ok(branches);
    }

    /// <summary>Returns commits for the project's GitHub repository.</summary>
    [HttpGet("commits")]
    public async Task<IActionResult> GetCommits(Guid projectId, [FromQuery] string? gitRef = null, [FromQuery] int page = 1)
    {
        var (repo, client) = await GetGitHubClientAsync(projectId);
        if (repo is null) return NotFound("Project not found or has no GitHub repository configured.");

        var url = $"{GitHubApiBase}/repos/{repo}/commits?per_page={CommitsPerPage}&page={page}";
        if (!string.IsNullOrWhiteSpace(gitRef))
            url += $"&sha={Uri.EscapeDataString(gitRef)}";

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Failed to fetch commits from GitHub.");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var commits = doc.RootElement.EnumerateArray().Select(c =>
        {
            var commit = c.GetProperty("commit");
            var author = commit.GetProperty("author");
            return new
            {
                sha = c.GetProperty("sha").GetString(),
                message = commit.GetProperty("message").GetString(),
                author = author.GetProperty("name").GetString(),
                authorEmail = author.GetProperty("email").GetString(),
                date = author.GetProperty("date").GetString(),
                url = c.GetProperty("html_url").GetString(),
            };
        }).ToList();

        return Ok(commits);
    }

    /// <summary>Returns the directory tree entries for the given ref and path.</summary>
    [HttpGet("tree")]
    public async Task<IActionResult> GetTree(Guid projectId, [FromQuery] string? gitRef = null, [FromQuery] string? path = null)
    {
        var (repo, client) = await GetGitHubClientAsync(projectId);
        if (repo is null) return NotFound("Project not found or has no GitHub repository configured.");

        var treePath = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim('/');
        var url = $"{GitHubApiBase}/repos/{repo}/contents/{Uri.EscapeDataString(treePath)}";
        if (!string.IsNullOrWhiteSpace(gitRef))
            url += $"?ref={Uri.EscapeDataString(gitRef)}";

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Failed to fetch tree from GitHub.");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        // If the response is an array, it's a directory listing; if object, it's a file
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return BadRequest("Path is a file, not a directory. Use the blob endpoint instead.");

        var entries = doc.RootElement.EnumerateArray().Select(e => new
        {
            name = e.GetProperty("name").GetString(),
            path = e.GetProperty("path").GetString(),
            type = e.GetProperty("type").GetString(),
            size = e.TryGetProperty("size", out var s) ? (int?)s.GetInt32() : null,
            sha = e.GetProperty("sha").GetString(),
        })
        .OrderBy(e => e.type == "dir" ? 0 : 1)
        .ThenBy(e => e.name)
        .ToList();

        return Ok(entries);
    }

    /// <summary>Returns the decoded content of a file blob.</summary>
    [HttpGet("blob")]
    public async Task<IActionResult> GetBlob(Guid projectId, [FromQuery] string? gitRef = null, [FromQuery] string? path = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest("path is required.");

        var (repo, client) = await GetGitHubClientAsync(projectId);
        if (repo is null) return NotFound("Project not found or has no GitHub repository configured.");

        var url = $"{GitHubApiBase}/repos/{repo}/contents/{Uri.EscapeDataString(path.Trim('/'))}";
        if (!string.IsNullOrWhiteSpace(gitRef))
            url += $"?ref={Uri.EscapeDataString(gitRef)}";

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Failed to fetch blob from GitHub.");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
            return BadRequest("Path is a directory. Use the tree endpoint instead.");

        var name = doc.RootElement.GetProperty("name").GetString();
        var filePath = doc.RootElement.GetProperty("path").GetString();
        var sha = doc.RootElement.GetProperty("sha").GetString();
        var size = doc.RootElement.GetProperty("size").GetInt32();
        var encoding = doc.RootElement.TryGetProperty("encoding", out var encProp)
            ? encProp.GetString() ?? "none"
            : "none";
        var content = doc.RootElement.TryGetProperty("content", out var contentProp)
            ? contentProp.GetString() ?? string.Empty
            : string.Empty;

        return Ok(new { name, path = filePath, sha, size, content, encoding });
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves the GitHub repo identifier and an authenticated HttpClient for the given project.
    /// Returns null repo if the project is not found or has no GitHub repo configured.
    /// </summary>
    private async Task<(string? repo, HttpClient client)> GetGitHubClientAsync(Guid projectId)
    {
        if (ctx.CurrentTenant is null)
            return (null, CreateClient(null));

        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == projectId && p.Organization.TenantId == ctx.CurrentTenant.Id);

        if (project is null || string.IsNullOrWhiteSpace(project.GitHubRepo))
            return (null, CreateClient(null));

        // Find the first linked GitHub identity for this project
        var identityLink = await db.GitHubIdentityProjects
            .Include(x => x.GitHubIdentity)
            .FirstOrDefaultAsync(x => x.ProjectId == projectId);

        string? token = null;
        if (identityLink is not null)
        {
            try
            {
                var protector = dpProvider.CreateProtector(ProtectorPurpose);
                token = protector.Unprotect(identityLink.GitHubIdentity.EncryptedToken);
            }
            catch (Exception ex)
            {
                // Token decryption failed — log and fall back to anonymous access
                logger.LogWarning(ex, "Failed to decrypt GitHub token for identity {IdentityId} on project {ProjectId}. Falling back to anonymous access.",
                    identityLink.GitHubIdentityId, projectId);
            }
        }

        return (project.GitHubRepo, CreateClient(token));
    }

    private HttpClient CreateClient(string? token)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("IssuePit/1.0");
        if (!string.IsNullOrWhiteSpace(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
