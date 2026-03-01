using System.Collections.Concurrent;
using IssuePit.Core.Entities;
using LibGit2Sharp;

namespace IssuePit.Api.Services;

public class GitService(ILogger<GitService> logger, IConfiguration configuration)
{
    private readonly string _reposBasePath = configuration["Git:ReposBasePath"]
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "issuepit", "repos");

    // Shared across all scoped instances so locks work regardless of DI lifetime.
    // One SemaphoreSlim per repository ID; the dictionary grows monotonically but is bounded
    // by the total number of git repositories, which is expected to be small.
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _repoLocks = new();

    private static SemaphoreSlim GetRepoLock(Guid repoId) =>
        _repoLocks.GetOrAdd(repoId, _ => new SemaphoreSlim(1, 1));

    public string GetLocalPath(GitRepository repo)    {
        if (!string.IsNullOrEmpty(repo.LocalPath))
            return repo.LocalPath;
        return Path.Combine(_reposBasePath, repo.ProjectId.ToString());
    }

    private FetchOptions BuildFetchOptions(GitRepository repo)
    {
        var opts = new FetchOptions();
        if (!string.IsNullOrEmpty(repo.AuthToken))
        {
            var user = string.IsNullOrEmpty(repo.AuthUsername) ? "git" : repo.AuthUsername;
            opts.CredentialsProvider = (_, _, _) =>
                new UsernamePasswordCredentials { Username = user, Password = repo.AuthToken };
        }
        else if (!string.IsNullOrEmpty(repo.AuthUsername))
        {
            opts.CredentialsProvider = (_, _, _) =>
                new UsernamePasswordCredentials { Username = repo.AuthUsername, Password = string.Empty };
        }
        return opts;
    }

    private CloneOptions BuildCloneOptions(GitRepository repo)
    {
        var opts = new CloneOptions { IsBare = false };
        if (!string.IsNullOrEmpty(repo.AuthToken))
        {
            var user = string.IsNullOrEmpty(repo.AuthUsername) ? "git" : repo.AuthUsername;
            opts.FetchOptions.CredentialsProvider = (_, _, _) =>
                new UsernamePasswordCredentials { Username = user, Password = repo.AuthToken };
        }
        else if (!string.IsNullOrEmpty(repo.AuthUsername))
        {
            opts.FetchOptions.CredentialsProvider = (_, _, _) =>
                new UsernamePasswordCredentials { Username = repo.AuthUsername, Password = string.Empty };
        }
        return opts;
    }

    /// <summary>Clones the remote repository to the local path if not already cloned. Internal: no locking.</summary>
    private string EnsureClonedCore(GitRepository repo)
    {
        var localPath = GetLocalPath(repo);
        if (Repository.IsValid(localPath))
            return localPath;

        Directory.CreateDirectory(localPath);
        logger.LogInformation("Cloning {RemoteUrl} to {LocalPath}", repo.RemoteUrl, localPath);
        Repository.Clone(repo.RemoteUrl, localPath, BuildCloneOptions(repo));
        return localPath;
    }

    /// <summary>Clones the remote repository to the local path if not already cloned.</summary>
    public string EnsureCloned(GitRepository repo)
    {
        var localPath = GetLocalPath(repo);
        // Fast path: already cloned, no lock needed
        if (Repository.IsValid(localPath))
            return localPath;

        // Slow path: acquire per-repo lock before cloning (serialises concurrent clone attempts)
        var sem = GetRepoLock(repo.Id);
        sem.Wait();
        try
        {
            return EnsureClonedCore(repo);
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>Clones the remote repository asynchronously, serialising concurrent requests per repository.</summary>
    public async Task<string> EnsureClonedAsync(GitRepository repo)
    {
        var localPath = GetLocalPath(repo);
        // Fast path: already cloned, no lock needed
        if (Repository.IsValid(localPath))
            return localPath;

        var sem = GetRepoLock(repo.Id);
        await sem.WaitAsync();
        try
        {
            return await Task.Run(() => EnsureClonedCore(repo));
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>Fetches latest changes from all remotes, serialising concurrent requests per repository.</summary>
    public async Task FetchAsync(GitRepository repo)
    {
        var sem = GetRepoLock(repo.Id);
        await sem.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                var localPath = EnsureClonedCore(repo);
                using var gitRepo = new Repository(localPath);
                foreach (var remote in gitRepo.Network.Remotes)
                {
                    var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification).ToArray();
                    Commands.Fetch(gitRepo, remote.Name, refSpecs, BuildFetchOptions(repo), null);
                    logger.LogInformation("Fetched from remote '{Remote}' for repo {Id}", remote.Name, repo.Id);
                }
            });
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>Returns all branches (local + remote).</summary>
    public IReadOnlyList<GitBranchInfo> GetBranches(GitRepository repo)
    {
        var localPath = EnsureCloned(repo);
        using var gitRepo = new Repository(localPath);
        return gitRepo.Branches
            .Select(b => new GitBranchInfo(
                b.FriendlyName,
                b.IsRemote,
                b.Tip?.Sha ?? string.Empty,
                b.Tip?.Author?.When.UtcDateTime))
            .ToList();
    }

    /// <summary>Returns commits for a given branch (or HEAD) with optional pagination.</summary>
    public IReadOnlyList<GitCommitInfo> GetCommits(GitRepository repo, string? branchName, int skip, int take)
    {
        var localPath = EnsureCloned(repo);
        using var gitRepo = new Repository(localPath);

        Commit? tip;
        if (string.IsNullOrEmpty(branchName))
        {
            tip = gitRepo.Head.Tip;
        }
        else
        {
            var branch = gitRepo.Branches.FirstOrDefault(b =>
                b.FriendlyName.Equals(branchName, StringComparison.OrdinalIgnoreCase) ||
                b.FriendlyName.Equals($"origin/{branchName}", StringComparison.OrdinalIgnoreCase));
            tip = branch?.Tip;
        }

        if (tip is null)
            return [];

        var filter = new CommitFilter { IncludeReachableFrom = tip };
        return gitRepo.Commits.QueryBy(filter)
            .Skip(skip)
            .Take(take)
            .Select(c => new GitCommitInfo(
                c.Sha,
                c.MessageShort,
                c.Message,
                c.Author?.Name ?? string.Empty,
                c.Author?.Email ?? string.Empty,
                c.Author?.When.UtcDateTime ?? DateTime.MinValue,
                c.Parents.Select(p => p.Sha).ToList()))
            .ToList();
    }

    /// <summary>Returns the tip commit SHA for the given branch, or null if the repo is not cloned or the branch is not found.</summary>
    public string? GetBranchTipSha(GitRepository repo, string branchName)
    {
        var localPath = GetLocalPath(repo);
        if (!Repository.IsValid(localPath))
            return null;

        using var gitRepo = new Repository(localPath);
        var branch = gitRepo.Branches.FirstOrDefault(b =>
            b.FriendlyName.Equals(branchName, StringComparison.OrdinalIgnoreCase) ||
            b.FriendlyName.Equals($"origin/{branchName}", StringComparison.OrdinalIgnoreCase));
        return branch?.Tip?.Sha;
    }

    /// <summary>Returns the directory tree at the given path for a branch/commit.</summary>
    public IReadOnlyList<GitTreeEntry> GetTree(GitRepository repo, string? branchOrSha, string? path)
    {
        var localPath = EnsureCloned(repo);
        using var gitRepo = new Repository(localPath);

        var commit = ResolveCommit(gitRepo, branchOrSha);
        if (commit is null)
            return [];

        var tree = commit.Tree;

        if (!string.IsNullOrEmpty(path))
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var entry = tree[part];
                if (entry?.TargetType == TreeEntryTargetType.Tree)
                    tree = (Tree)entry.Target;
                else
                    return [];
            }
        }

        return tree
            .Select(e => new GitTreeEntry(
                e.Name,
                e.Path,
                e.TargetType == TreeEntryTargetType.Tree ? "tree" : "blob",
                e.TargetType == TreeEntryTargetType.Blob ? ((Blob)e.Target).Size : 0))
            .OrderBy(e => e.Type == "tree" ? 0 : 1)
            .ThenBy(e => e.Name)
            .ToList();
    }

    /// <summary>Returns the raw content of a file blob.</summary>
    public GitBlobContent? GetBlob(GitRepository repo, string? branchOrSha, string path)
    {
        var localPath = EnsureCloned(repo);
        using var gitRepo = new Repository(localPath);

        var commit = ResolveCommit(gitRepo, branchOrSha);
        if (commit is null)
            return null;

        var entry = commit.Tree[path];
        if (entry?.TargetType != TreeEntryTargetType.Blob)
            return null;

        var blob = (Blob)entry.Target;
        var isBinary = blob.IsBinary;
        string content;
        if (isBinary)
            content = string.Empty;
        else
            content = blob.GetContentText();

        return new GitBlobContent(path, blob.Size, isBinary, content);
    }

    /// <summary>Returns the diff between two branches/commits as a list of changed files with parsed hunks.</summary>
    public IReadOnlyList<GitDiffFile> GetDiff(GitRepository repo, string baseBranch, string compareBranch, int contextLines = 3, int maxLinesPerFile = 2000)
    {
        var localPath = EnsureCloned(repo);
        using var gitRepo = new Repository(localPath);

        var baseCommit = ResolveCommit(gitRepo, baseBranch);
        var compareCommit = ResolveCommit(gitRepo, compareBranch);

        if (baseCommit is null || compareCommit is null)
            return [];

        var compareOptions = new CompareOptions { ContextLines = contextLines };
        var patch = gitRepo.Diff.Compare<Patch>(baseCommit.Tree, compareCommit.Tree, compareOptions);

        var result = new List<GitDiffFile>();
        foreach (PatchEntryChanges entry in patch)
        {
            var tooLarge = entry.LinesAdded + entry.LinesDeleted > maxLinesPerFile;
            var hunks = tooLarge ? (IReadOnlyList<GitDiffHunk>)[] : ParseHunks(entry.Patch);
            result.Add(new GitDiffFile(
                entry.OldPath,
                entry.Path,
                entry.Status.ToString(),
                entry.LinesAdded,
                entry.LinesDeleted,
                entry.IsBinaryComparison,
                tooLarge,
                hunks));
        }

        return result;
    }

    private static IReadOnlyList<GitDiffHunk> ParseHunks(string patchContent)
    {
        if (string.IsNullOrEmpty(patchContent)) return [];

        var hunks = new List<GitDiffHunk>();
        var rawLines = patchContent.Split('\n');

        int oldStart = 0, oldCount = 0, newStart = 0, newCount = 0;
        string hunkHeader = string.Empty;
        var currentLines = new List<GitDiffLine>();
        bool inHunk = false;
        int oldLine = 0, newLine = 0;

        foreach (var rawLine in rawLines)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                rawLine, @"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@(.*)$");
            if (m.Success)
            {
                if (inHunk)
                    hunks.Add(new GitDiffHunk(oldStart, oldCount, newStart, newCount, hunkHeader, currentLines.AsReadOnly()));

                oldStart = int.Parse(m.Groups[1].Value);
                oldCount = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 1;
                newStart = int.Parse(m.Groups[3].Value);
                newCount = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 1;
                hunkHeader = m.Groups[5].Value.Trim();
                currentLines = [];
                inHunk = true;
                oldLine = oldStart;
                newLine = newStart;
                continue;
            }

            if (!inHunk) continue;

            if (rawLine.StartsWith('+'))
            {
                currentLines.Add(new GitDiffLine(null, newLine++, rawLine[1..], "added"));
            }
            else if (rawLine.StartsWith('-'))
            {
                currentLines.Add(new GitDiffLine(oldLine++, null, rawLine[1..], "removed"));
            }
            else if (rawLine.Length == 0 || rawLine.StartsWith(' '))
            {
                string content = rawLine.Length > 0 ? rawLine[1..] : string.Empty;
                currentLines.Add(new GitDiffLine(oldLine++, newLine++, content, "context"));
            }
            // Skip lines like "\ No newline at end of file"
        }

        if (inHunk)
            hunks.Add(new GitDiffHunk(oldStart, oldCount, newStart, newCount, hunkHeader, currentLines.AsReadOnly()));

        return hunks;
    }

    private static Commit? ResolveCommit(Repository gitRepo, string? branchOrSha)
    {
        if (string.IsNullOrEmpty(branchOrSha))
            return gitRepo.Head.Tip;

        // Try as a branch name first
        var branch = gitRepo.Branches.FirstOrDefault(b =>
            b.FriendlyName.Equals(branchOrSha, StringComparison.OrdinalIgnoreCase) ||
            b.FriendlyName.Equals($"origin/{branchOrSha}", StringComparison.OrdinalIgnoreCase));
        if (branch?.Tip is not null)
            return branch.Tip;

        // Try as a commit SHA
        if (gitRepo.Lookup<Commit>(branchOrSha) is { } commit)
            return commit;

        return null;
    }
}

public record GitBranchInfo(string Name, bool IsRemote, string Sha, DateTime? CommitDate);
public record GitCommitInfo(string Sha, string MessageShort, string Message, string AuthorName, string AuthorEmail, DateTime Date, IList<string> ParentShas);
public record GitTreeEntry(string Name, string Path, string Type, long Size);
public record GitBlobContent(string Path, long Size, bool IsBinary, string Content);
public record GitDiffLine(int? OldLineNumber, int? NewLineNumber, string Content, string LineType);
public record GitDiffHunk(int OldStart, int OldCount, int NewStart, int NewCount, string Header, IReadOnlyList<GitDiffLine> Lines);
public record GitDiffFile(string OldPath, string NewPath, string Status, long AddedLines, long RemovedLines, bool IsBinary, bool IsTooLarge, IReadOnlyList<GitDiffHunk> Hunks);
