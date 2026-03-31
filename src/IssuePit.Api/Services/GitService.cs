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

    private PushOptions BuildPushOptions(GitRepository repo)
    {
        var opts = new PushOptions();
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

    /// <summary>Finds the remote in the local git repo whose URL matches <paramref name="repo"/>.RemoteUrl, or returns the first remote.</summary>
    private static Remote? FindMatchingRemote(Repository gitRepo, GitRepository repo) =>
        gitRepo.Network.Remotes.FirstOrDefault(r =>
            string.Equals(r.Url, repo.RemoteUrl, StringComparison.OrdinalIgnoreCase))
        ?? gitRepo.Network.Remotes.FirstOrDefault();

    /// <summary>
    /// Renames a diverged local branch by appending a date suffix (e.g. <c>main-pre-force-push-20260331</c>)
    /// so the old commits are preserved and the branch name becomes available for re-creation from the remote tip.
    /// </summary>
    private void RenameDivergedBranch(Repository gitRepo, Branch localBranch, Guid repoId)
    {
        var oldName = localBranch.FriendlyName;
        var suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var archiveName = $"{oldName}-pre-force-push-{suffix}";
        gitRepo.Branches.Rename(localBranch, archiveName);
        logger.LogWarning(
            "Renamed diverged branch '{OldName}' → '{NewName}' for repo {Id} (remote was force-pushed)",
            oldName, archiveName, repoId);
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

    /// <summary>Fetches latest changes from all remotes and fast-forwards local tracking branches,
    /// serialising concurrent requests per repository.</summary>
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

                // Fast-forward local tracking branches that have not diverged from their remote counterpart.
                foreach (var localBranch in gitRepo.Branches.Where(b => !b.IsRemote && b.TrackedBranch != null).ToList())
                {
                    var tracked = localBranch.TrackedBranch;
                    if (tracked?.Tip == null || localBranch.Tip == null) continue;
                    if (localBranch.Tip.Sha == tracked.Tip.Sha) continue;

                    var divergence = gitRepo.ObjectDatabase.CalculateHistoryDivergence(localBranch.Tip, tracked.Tip);
                    if (divergence.AheadBy == 0)
                    {
                        gitRepo.Refs.UpdateTarget(localBranch.Reference, tracked.Tip.Id);
                        logger.LogInformation("Fast-forwarded '{Branch}' to '{Sha}' for repo {Id}", localBranch.FriendlyName, tracked.Tip.Sha, repo.Id);
                    }
                    else
                    {
                        // Remote was force-pushed: rename the diverged local branch to preserve
                        // the old commits, then recreate it from the remote-tracking tip.
                        var branchName = localBranch.FriendlyName;
                        var trackedCanonical = tracked.CanonicalName;
                        RenameDivergedBranch(gitRepo, localBranch, repo.Id);
                        var newBranch = gitRepo.CreateBranch(branchName, tracked.Tip);
                        gitRepo.Branches.Update(newBranch, b => b.TrackedBranch = trackedCanonical);
                        logger.LogInformation("Reset diverged '{Branch}' to remote tip '{Sha}' for repo {Id} (old branch archived)", branchName, tracked.Tip.Sha, repo.Id);
                    }
                }
            });
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Pulls (fetch + fast-forward) the given branch from the remote that matches
    /// <paramref name="repo"/>.RemoteUrl, serialising concurrent requests per repository.
    /// </summary>
    public async Task PullAsync(GitRepository repo, string? branch = null)
    {
        var branchName = branch ?? repo.DefaultBranch;
        var sem = GetRepoLock(repo.Id);
        await sem.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                var localPath = EnsureClonedCore(repo);
                using var gitRepo = new Repository(localPath);

                var remote = FindMatchingRemote(gitRepo, repo)
                    ?? throw new InvalidOperationException("No remote configured in repository.");

                // Fetch from the matched remote
                var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification).ToArray();
                Commands.Fetch(gitRepo, remote.Name, refSpecs, BuildFetchOptions(repo), null);

                // Resolve the remote-tracking branch (e.g. origin/main)
                var remoteBranch = gitRepo.Branches[$"{remote.Name}/{branchName}"]
                    ?? throw new InvalidOperationException($"Remote branch '{remote.Name}/{branchName}' not found after fetch.");

                // Find or create the local tracking branch, then fast-forward
                var localBranch = gitRepo.Branches[branchName];
                if (localBranch == null)
                {
                    localBranch = gitRepo.CreateBranch(branchName, remoteBranch.Tip);
                    gitRepo.Branches.Update(localBranch, b => b.TrackedBranch = remoteBranch.CanonicalName);
                }
                else
                {
                    // Verify this is a safe fast-forward (local must not be ahead of remote)
                    var divergence = gitRepo.ObjectDatabase.CalculateHistoryDivergence(localBranch.Tip, remoteBranch.Tip);
                    if (divergence.AheadBy > 0)
                    {
                        // Remote was force-pushed: rename the diverged local branch to preserve
                        // the old commits, then recreate the branch from the remote tip.
                        RenameDivergedBranch(gitRepo, localBranch, repo.Id);
                        localBranch = gitRepo.CreateBranch(branchName, remoteBranch.Tip);
                        gitRepo.Branches.Update(localBranch, b => b.TrackedBranch = remoteBranch.CanonicalName);
                    }
                    else
                    {
                        gitRepo.Refs.UpdateTarget(localBranch.Reference, remoteBranch.Tip.Id);
                    }
                }

                logger.LogInformation("Pulled '{Branch}' from remote '{Remote}' for repo {Id}", branchName, remote.Name, repo.Id);
            });
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Pushes the given branch to the remote that matches <paramref name="repo"/>.RemoteUrl,
    /// serialising concurrent requests per repository.
    /// </summary>
    public async Task PushAsync(GitRepository repo, string? branch = null)
    {
        var branchName = branch ?? repo.DefaultBranch;
        var sem = GetRepoLock(repo.Id);
        await sem.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                var localPath = EnsureClonedCore(repo);
                using var gitRepo = new Repository(localPath);

                var remote = FindMatchingRemote(gitRepo, repo)
                    ?? throw new InvalidOperationException("No remote configured in repository.");

                var refspec = $"refs/heads/{branchName}:refs/heads/{branchName}";
                gitRepo.Network.Push(remote, refspec, BuildPushOptions(repo));

                logger.LogInformation("Pushed '{Branch}' to remote '{Remote}' for repo {Id}", branchName, remote.Name, repo.Id);
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

    /// <summary>
    /// Returns the total number of commits reachable from the tip of <paramref name="branchName"/>,
    /// or <c>null</c> if the repo is not cloned, the branch is not found, or the count cannot be
    /// determined. Used by the agent runtime to select the clone source with the deepest commit chain.
    /// </summary>
    public int? GetBranchCommitCount(GitRepository repo, string branchName)
    {
        var localPath = GetLocalPath(repo);
        if (!Repository.IsValid(localPath))
            return null;

        using var gitRepo = new Repository(localPath);
        var branch = gitRepo.Branches.FirstOrDefault(b =>
            b.FriendlyName.Equals(branchName, StringComparison.OrdinalIgnoreCase) ||
            b.FriendlyName.Equals($"origin/{branchName}", StringComparison.OrdinalIgnoreCase));
        if (branch?.Tip == null)
            return null;

        // Use git rev-list --count <sha> subprocess — fastest method even for very large repos
        // because git uses pack-index / commit-graph files for O(1) reachability counts.
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = localPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.StartInfo.ArgumentList.Add("rev-list");
            process.StartInfo.ArgumentList.Add("--count");
            process.StartInfo.ArgumentList.Add(branch.Tip.Sha);
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                logger.LogWarning(
                    "git rev-list --count failed for repo {RepoId} branch '{Branch}' (exit {ExitCode}): {Stderr}",
                    repo.Id, branchName, process.ExitCode, stderr.Trim());
                return null;
            }
            return int.TryParse(output.Trim(), out var count) ? count : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to count commits for repo {RepoId} branch '{Branch}'", repo.Id, branchName);
            return null;
        }
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

    /// <summary>
    /// Merges <paramref name="sourceBranch"/> into <paramref name="targetBranch"/> using a merge commit.
    /// Returns the resulting merge commit SHA, or throws on conflict / failure.
    /// </summary>
    public string MergeBranch(GitRepository repo, string sourceBranch, string targetBranch, string committerName = "IssuePit", string committerEmail = "issuepit@localhost")
    {
        var localPath = EnsureCloned(repo);
        var sem = GetRepoLock(repo.Id);
        sem.Wait();
        try
        {
            using var gitRepo = new Repository(localPath);

            // Resolve source and target commits
            var sourceCommit = ResolveCommit(gitRepo, sourceBranch)
                ?? throw new InvalidOperationException($"Source branch '{sourceBranch}' not found.");
            var targetCommit = ResolveCommit(gitRepo, targetBranch)
                ?? throw new InvalidOperationException($"Target branch '{targetBranch}' not found.");

            var localTarget = CheckoutLocalTarget(gitRepo, targetBranch);
            Commands.Checkout(gitRepo, localTarget);

            var committer = new Signature(committerName, committerEmail, DateTimeOffset.UtcNow);
            var mergeResult = gitRepo.Merge(sourceCommit, committer, new MergeOptions
            {
                FastForwardStrategy = FastForwardStrategy.NoFastForward,
                CommitOnSuccess = true,
            });

            if (mergeResult.Status == MergeStatus.Conflicts)
                throw new InvalidOperationException("Merge resulted in conflicts and cannot be auto-merged.");

            if (mergeResult.Status == MergeStatus.UpToDate)
                return targetCommit.Sha;

            return mergeResult.Commit?.Sha
                ?? gitRepo.Head.Tip?.Sha
                ?? throw new InvalidOperationException("Merge completed but commit SHA is unavailable.");
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Squash-merges <paramref name="sourceBranch"/> into <paramref name="targetBranch"/>.
    /// All source commits are combined into a single commit on the target branch.
    /// Returns the resulting commit SHA, or throws on conflict / failure.
    /// </summary>
    public string SquashMergeBranch(GitRepository repo, string sourceBranch, string targetBranch, string committerName = "IssuePit", string committerEmail = "issuepit@localhost")
    {
        var localPath = EnsureCloned(repo);
        var sem = GetRepoLock(repo.Id);
        sem.Wait();
        try
        {
            using var gitRepo = new Repository(localPath);

            var sourceCommit = ResolveCommit(gitRepo, sourceBranch)
                ?? throw new InvalidOperationException($"Source branch '{sourceBranch}' not found.");
            var targetCommit = ResolveCommit(gitRepo, targetBranch)
                ?? throw new InvalidOperationException($"Target branch '{targetBranch}' not found.");

            var localTarget = CheckoutLocalTarget(gitRepo, targetBranch);
            Commands.Checkout(gitRepo, localTarget);

            // Perform squash merge: merge with commit, then reset to create a single-parent commit
            var committer = new Signature(committerName, committerEmail, DateTimeOffset.UtcNow);
            var mergeResult = gitRepo.Merge(sourceCommit, committer, new MergeOptions
            {
                FastForwardStrategy = FastForwardStrategy.NoFastForward,
                CommitOnSuccess = true,
            });

            if (mergeResult.Status == MergeStatus.Conflicts)
                throw new InvalidOperationException("Merge resulted in conflicts and cannot be auto-merged.");

            if (mergeResult.Status == MergeStatus.UpToDate)
                return targetCommit.Sha;

            // The merge created a two-parent commit — recreate as a single-parent (squash) commit
            var mergedTree = mergeResult.Commit?.Tree ?? gitRepo.Head.Tip?.Tree
                ?? throw new InvalidOperationException("Squash merge completed but tree is unavailable.");

            var squashCommit = gitRepo.ObjectDatabase.CreateCommit(
                committer, committer,
                $"Squashed commit of {sourceBranch} into {targetBranch}",
                mergedTree,
                new[] { targetCommit },
                prettifyMessage: false);

            // Update HEAD / branch ref to point to the squash commit
            gitRepo.Refs.UpdateTarget(gitRepo.Head.Reference, squashCommit.Id);
            gitRepo.Reset(ResetMode.Hard, squashCommit);

            return squashCommit.Sha;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Rebase-merges <paramref name="sourceBranch"/> onto <paramref name="targetBranch"/>.
    /// Replays source branch commits on top of the target, producing a linear history.
    /// Returns the resulting tip commit SHA, or throws on conflict / failure.
    /// </summary>
    public string RebaseMergeBranch(GitRepository repo, string sourceBranch, string targetBranch, string committerName = "IssuePit", string committerEmail = "issuepit@localhost")
    {
        var localPath = EnsureCloned(repo);
        var sem = GetRepoLock(repo.Id);
        sem.Wait();
        try
        {
            using var gitRepo = new Repository(localPath);

            var sourceCommit = ResolveCommit(gitRepo, sourceBranch)
                ?? throw new InvalidOperationException($"Source branch '{sourceBranch}' not found.");
            var targetCommit = ResolveCommit(gitRepo, targetBranch)
                ?? throw new InvalidOperationException($"Target branch '{targetBranch}' not found.");

            // Already up to date
            if (sourceCommit.Sha == targetCommit.Sha)
                return targetCommit.Sha;

            var localTarget = CheckoutLocalTarget(gitRepo, targetBranch);
            Commands.Checkout(gitRepo, localTarget);

            // Use fast-forward merge strategy — if rebase is clean this produces a linear result
            var committer = new Signature(committerName, committerEmail, DateTimeOffset.UtcNow);
            var mergeResult = gitRepo.Merge(sourceCommit, committer, new MergeOptions
            {
                FastForwardStrategy = FastForwardStrategy.Default,
                CommitOnSuccess = true,
            });

            if (mergeResult.Status == MergeStatus.Conflicts)
                throw new InvalidOperationException("Rebase resulted in conflicts and cannot be auto-merged.");

            if (mergeResult.Status == MergeStatus.UpToDate)
                return targetCommit.Sha;

            return mergeResult.Commit?.Sha
                ?? gitRepo.Head.Tip?.Sha
                ?? throw new InvalidOperationException("Rebase completed but commit SHA is unavailable.");
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Deletes a local branch. Typically called after a successful merge to clean up the source branch.
    /// </summary>
    public void DeleteLocalBranch(GitRepository repo, string branchName)
    {
        var localPath = GetLocalPath(repo);
        if (!Repository.IsValid(localPath)) return;

        var sem = GetRepoLock(repo.Id);
        sem.Wait();
        try
        {
            using var gitRepo = new Repository(localPath);
            var branch = gitRepo.Branches[branchName];
            if (branch is not null && !branch.IsRemote)
                gitRepo.Branches.Remove(branch);
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>Checks out or creates a local tracking branch for the given branch name.</summary>
    private static Branch CheckoutLocalTarget(Repository gitRepo, string targetBranch)
    {
        var target = gitRepo.Branches.FirstOrDefault(b =>
            b.FriendlyName.Equals(targetBranch, StringComparison.OrdinalIgnoreCase) ||
            b.FriendlyName.Equals($"origin/{targetBranch}", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Target branch '{targetBranch}' not found in repository.");

        if (target.IsRemote)
        {
            var localName = targetBranch;
            var existing = gitRepo.Branches[localName];
            if (existing is null)
            {
                existing = gitRepo.CreateBranch(localName, target.Tip);
                gitRepo.Branches.Update(existing, b => b.TrackedBranch = target.CanonicalName);
            }
            return existing;
        }

        return target;
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
