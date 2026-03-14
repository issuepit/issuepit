using System.Text.RegularExpressions;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Scans git repositories for branch names and commit messages that reference IssuePit or
/// GitHub issues, and persists <see cref="IssueGitMapping"/> records for each match found.
/// </summary>
/// <remarks>
/// Supported patterns:
/// <list type="bullet">
///   <item>Branch name: number after slash — e.g. <c>fix/69-something</c>, <c>feat/ip-123-another</c>, <c>feat/ip123-branch</c></item>
///   <item>Commit message: IssuePit issue key — e.g. <c>IP-123</c>, <c>ip-123</c>, <c>ip123</c></item>
///   <item>Commit message: GitHub issue reference — e.g. <c>#123</c>, <c>closes #123</c>, <c>fixes #123</c></item>
/// </list>
/// </remarks>
public partial class BranchDetectionService(
    IssuePitDbContext db,
    GitService gitService,
    IConfiguration configuration,
    ILogger<BranchDetectionService> logger)
{
    // Matches an IssuePit issue reference in a branch segment: ip-123 or ip123
    // Also matches a plain number at the start of the segment: 69-something → 69
    [GeneratedRegex(@"(?:ip-?(\d+)|^(\d+))", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BranchIssueNumberRegex();

    // Matches IssuePit issue keys in commit messages: IP-123, ip-123, ip123
    [GeneratedRegex(@"\bip-?(\d+)\b", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CommitIssuePitRegex();

    // Matches GitHub issue references in commit messages: #123 (standalone number references)
    [GeneratedRegex(@"(?:closes?|fixes?|resolves?)?\s*#(\d+)", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CommitGitHubRegex();

    /// <summary>
    /// Scans all git repositories for all projects and creates/refreshes <see cref="IssueGitMapping"/>
    /// records for any issue references found in branch names or recent commit messages.
    /// </summary>
    public async Task DetectAsync(CancellationToken cancellationToken)
    {
        var repos = await db.GitRepositories
            .Where(r => r.Status != GitRepoStatus.Disabled)
            .ToListAsync(cancellationToken);

        logger.LogInformation("BranchDetection: scanning {Count} repository/repositories", repos.Count);

        foreach (var repo in repos)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                await ScanRepositoryAsync(repo, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "BranchDetection: failed for repo {RepoId}", repo.Id);
            }
        }
    }

    private async Task ScanRepositoryAsync(GitRepository repo, CancellationToken cancellationToken)
    {
        // Load all issues for this project so we can resolve issue numbers → IDs.
        var issues = await db.Issues
            .Where(i => i.ProjectId == repo.ProjectId)
            .Select(i => new { i.Id, i.Number, i.GitHubIssueNumber })
            .ToListAsync(cancellationToken);

        if (issues.Count == 0)
        {
            logger.LogDebug("BranchDetection: repo {RepoId} has no issues — skipping", repo.Id);
            return;
        }

        // Build lookup maps: IssuePit number → issue id, GitHub number → issue id.
        var byNumber = issues.ToDictionary(i => i.Number, i => i.Id);
        var byGitHub = issues
            .Where(i => i.GitHubIssueNumber.HasValue)
            .GroupBy(i => i.GitHubIssueNumber!.Value)
            .ToDictionary(g => g.Key, g => g.First().Id);

        // Fetch existing mappings for this repo so we can avoid duplicates.
        var existingBranchMappings = await db.IssueGitMappings
            .Where(m => m.RepositoryId == repo.Id && m.Source == IssueGitMappingSource.BranchName)
            .Select(m => new { m.IssueId, m.BranchName })
            .ToListAsync(cancellationToken);

        var existingCommitMappings = await db.IssueGitMappings
            .Where(m => m.RepositoryId == repo.Id && m.Source == IssueGitMappingSource.CommitMessage)
            .Select(m => new { m.IssueId, m.CommitSha })
            .ToListAsync(cancellationToken);

        var knownBranches = existingBranchMappings
            .Select(m => (m.IssueId, BranchName: m.BranchName))
            .ToHashSet();

        var knownCommits = existingCommitMappings
            .Select(m => (m.IssueId, CommitSha: m.CommitSha))
            .ToHashSet();

        int newMappings = 0;

        // ── Branch name detection ──────────────────────────────────────────────
        IReadOnlyList<GitBranchInfo> branches;
        try
        {
            branches = gitService.GetBranches(repo);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "BranchDetection: could not read branches for repo {RepoId}", repo.Id);
            return;
        }

        foreach (var branch in branches)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var issueIds = ResolveBranchIssueIds(branch.Name, byNumber, byGitHub);
            foreach (var issueId in issueIds)
            {
                var key = (issueId, BranchName: (string?)branch.Name);
                if (knownBranches.Contains(key)) continue;

                db.IssueGitMappings.Add(new IssueGitMapping
                {
                    Id = Guid.NewGuid(),
                    IssueId = issueId,
                    RepositoryId = repo.Id,
                    BranchName = branch.Name,
                    Source = IssueGitMappingSource.BranchName,
                    DetectedAt = DateTime.UtcNow,
                });
                knownBranches.Add(key);
                newMappings++;

                logger.LogDebug(
                    "BranchDetection: mapped branch '{Branch}' → issue {IssueId} in repo {RepoId}",
                    branch.Name, issueId, repo.Id);
            }
        }

        // ── Commit message detection ───────────────────────────────────────────
        // Scan a bounded window of recent commits across all branches.
        var commitsToScanPerBranch = configuration.GetValue("BranchDetection:CommitsToScanPerBranch", 100);

        foreach (var branch in branches)
        {
            if (cancellationToken.IsCancellationRequested) break;

            IReadOnlyList<GitCommitInfo> commits;
            try
            {
                commits = gitService.GetCommits(repo, branch.Name, skip: 0, take: commitsToScanPerBranch);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "BranchDetection: could not read commits for branch '{Branch}' in repo {RepoId}", branch.Name, repo.Id);
                continue;
            }

            foreach (var commit in commits)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var issueIds = ResolveCommitIssueIds(commit.Message, byNumber, byGitHub);
                foreach (var issueId in issueIds)
                {
                    var key = (issueId, CommitSha: (string?)commit.Sha);
                    if (knownCommits.Contains(key)) continue;

                    db.IssueGitMappings.Add(new IssueGitMapping
                    {
                        Id = Guid.NewGuid(),
                        IssueId = issueId,
                        RepositoryId = repo.Id,
                        CommitSha = commit.Sha,
                        Source = IssueGitMappingSource.CommitMessage,
                        DetectedAt = DateTime.UtcNow,
                    });
                    knownCommits.Add(key);
                    newMappings++;

                    logger.LogDebug(
                        "BranchDetection: mapped commit {Sha} → issue {IssueId} in repo {RepoId}",
                        commit.Sha, issueId, repo.Id);
                }
            }
        }

        if (newMappings > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "BranchDetection: added {Count} new mapping(s) for repo {RepoId}",
                newMappings, repo.Id);
        }
    }

    /// <summary>
    /// Extracts issue IDs referenced in a branch name segment after the last slash.
    /// Handles patterns: <c>fix/69-something</c>, <c>feat/ip-123-x</c>, <c>feat/ip123-x</c>.
    /// </summary>
    public static IEnumerable<Guid> ResolveBranchIssueIds(
        string branchName,
        Dictionary<int, Guid> byNumber,
        Dictionary<int, Guid> byGitHub)
    {
        // Only inspect the part after the last slash (e.g. "69-my-fix" from "fix/69-my-fix")
        // Trim trailing slashes so "fix/123/" is treated the same as "fix/123".
        var trimmed = branchName.TrimEnd('/');
        var segment = trimmed.Contains('/')
            ? trimmed[(trimmed.LastIndexOf('/') + 1)..]
            : trimmed;

        var matched = new HashSet<Guid>();
        foreach (Match m in BranchIssueNumberRegex().Matches(segment))
        {
            // Group 1: ip-?NNN  Group 2: plain NNN at start
            var numStr = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            if (!int.TryParse(numStr, out var num)) continue;

            if (byNumber.TryGetValue(num, out var id))
                matched.Add(id);
            // GitHub issue numbers are also valid references for branch names
            else if (byGitHub.TryGetValue(num, out var ghId))
                matched.Add(ghId);
        }

        return matched;
    }

    /// <summary>
    /// Extracts issue IDs referenced in a commit message.
    /// Handles: <c>IP-123</c>, <c>ip-123</c>, <c>ip123</c>, <c>#123</c>, <c>closes #123</c>.
    /// </summary>
    public static IEnumerable<Guid> ResolveCommitIssueIds(
        string message,
        Dictionary<int, Guid> byNumber,
        Dictionary<int, Guid> byGitHub)
    {
        var matched = new HashSet<Guid>();

        // IssuePit references: ip-NNN or ipNNN
        foreach (Match m in CommitIssuePitRegex().Matches(message))
        {
            if (!int.TryParse(m.Groups[1].Value, out var num)) continue;
            if (byNumber.TryGetValue(num, out var id))
                matched.Add(id);
        }

        // GitHub references: #NNN, closes #NNN, fixes #NNN
        foreach (Match m in CommitGitHubRegex().Matches(message))
        {
            if (!int.TryParse(m.Groups[1].Value, out var num)) continue;
            if (byGitHub.TryGetValue(num, out var id))
                matched.Add(id);
        }

        return matched;
    }
}
