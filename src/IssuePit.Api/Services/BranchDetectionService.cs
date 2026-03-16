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
///   <item>Branch name: number after slash — e.g. <c>fix/69-something</c></item>
///   <item>Branch name: project slug prefix — e.g. <c>feat/{slug}-123-another</c> or <c>feat/{slug}123-branch</c></item>
///   <item>Commit message: project issue key — e.g. <c>{SLUG}-123</c> or <c>{SLUG}123</c></item>
///   <item>Commit message: GitHub issue reference — e.g. <c>#123</c>, <c>closes #123</c>, <c>fixes #123</c></item>
/// </list>
/// </remarks>
public partial class BranchDetectionService(
    IssuePitDbContext db,
    GitService gitService,
    IConfiguration configuration,
    ILogger<BranchDetectionService> logger)
{
    // Plain number at the start of a branch name segment: fix/69-something → 69
    [GeneratedRegex(@"^(\d+)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BranchPlainNumberRegex();

    // Matches GitHub issue references in commit messages: #123, closes #123, fixes #123, resolves #123
    [GeneratedRegex(@"(?:closes?|fixes?|resolves?)?\s*#(\d+)", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CommitGitHubRegex();

    // Dynamic regex for the project's issue key in branch name segments (e.g. "IP-123" or "IP123")
    private static Regex BuildSlugBranchRegex(string issueKey) =>
        new($@"{Regex.Escape(issueKey)}-?(\d+)", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    // Dynamic regex for the project's issue key in commit messages (e.g. "IP-123" or "IP123")
    private static Regex BuildSlugCommitRegex(string issueKey) =>
        new($@"\b{Regex.Escape(issueKey)}-?(\d+)\b", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Scans all git repositories for all projects and creates/refreshes <see cref="IssueGitMapping"/>
    /// records for any issue references found in branch names or recent commit messages.
    /// Creates a <see cref="BranchDetectionRun"/> audit record per project.
    /// </summary>
    public async Task DetectAsync(CancellationToken cancellationToken)
    {
        var repos = await db.GitRepositories
            .Where(r => r.Status != GitRepoStatus.Disabled)
            .ToListAsync(cancellationToken);

        logger.LogInformation("BranchDetection: scanning {Count} repository/repositories", repos.Count);

        // Group repositories by project so we create one run record per project.
        var reposByProject = repos.GroupBy(r => r.ProjectId).ToList();

        foreach (var group in reposByProject)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var projectId = group.Key;
            var run = new BranchDetectionRun
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Status = GitHubSyncRunStatus.Running,
                StartedAt = DateTime.UtcNow,
            };
            db.BranchDetectionRuns.Add(run);
            await db.SaveChangesAsync(cancellationToken);

            int projectMappings = 0;
            bool failed = false;

            foreach (var repo in group)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    projectMappings += await ScanRepositoryAsync(repo, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "BranchDetection: failed for repo {RepoId}", repo.Id);
                    failed = true;
                }
            }

            run.Status = failed ? GitHubSyncRunStatus.Failed : GitHubSyncRunStatus.Succeeded;
            run.Summary = projectMappings > 0
                ? $"{projectMappings} new mapping(s)"
                : "No new mappings";
            run.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<int> ScanRepositoryAsync(GitRepository repo, CancellationToken cancellationToken)
    {
        // Load the project to get its IssueKey (the short slug prefix, e.g. "IP", "PROJ").
        var project = await db.Projects.FindAsync([repo.ProjectId], cancellationToken);
        var issueKey = project?.IssueKey;

        // Load all issues for this project so we can resolve issue numbers → IDs.
        var issues = await db.Issues
            .Where(i => i.ProjectId == repo.ProjectId)
            .Select(i => new { i.Id, i.Number, i.GitHubIssueNumber })
            .ToListAsync(cancellationToken);

        if (issues.Count == 0)
        {
            logger.LogDebug("BranchDetection: repo {RepoId} has no issues — skipping", repo.Id);
            return 0;
        }

        // Build lookup maps: IssuePit number → issue id, GitHub number → issue id.
        var byNumber = issues.ToDictionary(i => i.Number, i => i.Id);
        var byGitHub = issues
            .Where(i => i.GitHubIssueNumber.HasValue)
            .GroupBy(i => i.GitHubIssueNumber!.Value)
            .ToDictionary(g => g.Key, g => g.First().Id);

        // Build slug regexes once per project (null when no IssueKey is configured).
        Regex? slugBranchRegex = string.IsNullOrEmpty(issueKey) ? null : BuildSlugBranchRegex(issueKey);
        Regex? slugCommitRegex = string.IsNullOrEmpty(issueKey) ? null : BuildSlugCommitRegex(issueKey);

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

        // All commit SHAs already mapped — used as a scan watermark to stop early.
        // Commits are iterated newest-first; the first already-known SHA signals that all
        // older commits on this branch were processed in a previous run.
        var alreadyMappedShas = existingCommitMappings
            .Where(m => m.CommitSha != null)
            .Select(m => m.CommitSha!)
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
            return 0;
        }

        foreach (var branch in branches)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var issueIds = ResolveBranchIssueIds(branch.Name, byNumber, byGitHub, slugBranchRegex);
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
        // Scan commits per branch newest-first; stop early when reaching a commit that was
        // already processed in a previous run (i.e. its SHA is in alreadyMappedShas).
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

                // Stop scanning: we've already processed this commit (and all older ones) in a previous run.
                if (alreadyMappedShas.Contains(commit.Sha))
                    break;

                var issueIds = ResolveCommitIssueIds(commit.Message, byNumber, byGitHub, slugCommitRegex);
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

        return newMappings;
    }

    /// <summary>
    /// Extracts issue IDs referenced in a branch name segment after the last slash.
    /// Handles patterns: <c>fix/69-something</c> (plain number) and <c>feat/{slug}-123-x</c> / <c>feat/{slug}123-x</c> (slug-based).
    /// </summary>
    /// <param name="branchName">Full branch name.</param>
    /// <param name="byNumber">Map from IssuePit issue number to issue ID.</param>
    /// <param name="byGitHub">Map from linked GitHub issue number to issue ID.</param>
    /// <param name="slugRegex">Pre-built regex for the project's issue key slug. Null means skip slug matching.</param>
    public static IEnumerable<Guid> ResolveBranchIssueIds(
        string branchName,
        Dictionary<int, Guid> byNumber,
        Dictionary<int, Guid> byGitHub,
        Regex? slugRegex = null)
    {
        // Only inspect the part after the last slash (e.g. "69-my-fix" from "fix/69-my-fix")
        // Trim trailing slashes so "fix/123/" is treated the same as "fix/123".
        var trimmed = branchName.TrimEnd('/');
        var segment = trimmed.Contains('/')
            ? trimmed[(trimmed.LastIndexOf('/') + 1)..]
            : trimmed;

        var matched = new HashSet<Guid>();

        // Plain number at the start of the segment: 69-my-fix → 69
        var plainMatch = BranchPlainNumberRegex().Match(segment);
        if (plainMatch.Success && int.TryParse(plainMatch.Groups[1].Value, out var plainNum))
        {
            if (byNumber.TryGetValue(plainNum, out var id)) matched.Add(id);
            else if (byGitHub.TryGetValue(plainNum, out var ghId)) matched.Add(ghId);
        }

        // Project slug-based: {slug}-123 or {slug}123
        if (slugRegex != null)
        {
            foreach (Match m in slugRegex.Matches(segment))
            {
                if (!int.TryParse(m.Groups[1].Value, out var num)) continue;
                if (byNumber.TryGetValue(num, out var id)) matched.Add(id);
                else if (byGitHub.TryGetValue(num, out var ghId)) matched.Add(ghId);
            }
        }

        return matched;
    }

    /// <summary>
    /// Extracts issue IDs referenced in a commit message.
    /// Handles: project slug key (e.g. <c>{SLUG}-123</c>, <c>{SLUG}123</c>), <c>#123</c>, <c>closes #123</c>.
    /// </summary>
    /// <param name="message">Full commit message text.</param>
    /// <param name="byNumber">Map from IssuePit issue number to issue ID.</param>
    /// <param name="byGitHub">Map from linked GitHub issue number to issue ID.</param>
    /// <param name="slugRegex">Pre-built regex for the project's issue key slug. Null means skip slug matching.</param>
    public static IEnumerable<Guid> ResolveCommitIssueIds(
        string message,
        Dictionary<int, Guid> byNumber,
        Dictionary<int, Guid> byGitHub,
        Regex? slugRegex = null)
    {
        var matched = new HashSet<Guid>();

        // Project slug-based issue key references: {SLUG}-NNN or {SLUG}NNN
        if (slugRegex != null)
        {
            foreach (Match m in slugRegex.Matches(message))
            {
                if (!int.TryParse(m.Groups[1].Value, out var num)) continue;
                if (byNumber.TryGetValue(num, out var id)) matched.Add(id);
            }
        }

        // GitHub references: #NNN, closes #NNN, fixes #NNN, resolves #NNN
        foreach (Match m in CommitGitHubRegex().Matches(message))
        {
            if (!int.TryParse(m.Groups[1].Value, out var num)) continue;
            if (byGitHub.TryGetValue(num, out var id)) matched.Add(id);
        }

        return matched;
    }
}
