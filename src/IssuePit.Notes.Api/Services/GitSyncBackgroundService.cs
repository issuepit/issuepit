using IssuePit.Notes.Core.Data;
using IssuePit.Notes.Core.Entities;
using IssuePit.Notes.Core.Enums;
using LibGit2Sharp;
using Microsoft.EntityFrameworkCore;
using NoteEntity = IssuePit.Notes.Core.Entities.Note;

namespace IssuePit.Notes.Api.Services;

/// <summary>
/// Background service that periodically syncs git-backed notebooks using LibGit2Sharp.
/// Clones the remote repository on first run, pulls changes on subsequent runs,
/// imports new/changed markdown files into the database, exports DB changes to files,
/// and commits + pushes the result back to the remote.
/// </summary>
public class GitSyncBackgroundService(
    ILogger<GitSyncBackgroundService> logger,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(
        configuration.GetValue("GitSync:IntervalSeconds", 120));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("GitSyncBackgroundService started; interval = {Interval}s", _interval.TotalSeconds);

        // Startup delay — wait for the application to fully initialise
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAllAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unhandled error in GitSyncBackgroundService");
            }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        logger.LogInformation("GitSyncBackgroundService stopped");
    }

    private async Task SyncAllAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();

        var gitNotebooks = await db.Notebooks
            .Where(n => n.StorageProvider == StorageProvider.Git
                     && n.GitRepoUrl != null
                     && n.GitRepoUrl != "")
            .ToListAsync(ct);

        if (gitNotebooks.Count == 0) return;

        logger.LogInformation("Syncing {Count} git-backed notebook(s)", gitNotebooks.Count);

        foreach (var notebook in gitNotebooks)
        {
            try
            {
                // LibGit2Sharp is synchronous — run on thread pool to avoid blocking the async loop
                await Task.Run(() => SyncNotebook(notebook, db), ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Git sync failed for notebook '{Name}' ({Id})", notebook.Name, notebook.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private void SyncNotebook(Notebook notebook, NotesDbContext db)
    {
        var workDir = GetWorkDir(notebook);
        var branch = notebook.GitBranch ?? "main";
        var repoUrl = notebook.GitRepoUrl!;

        if (!Repository.IsValid(workDir))
        {
            logger.LogInformation("Cloning git repo for notebook '{Name}' ({Id})", notebook.Name, notebook.Id);
            Directory.CreateDirectory(workDir);
            Repository.Clone(repoUrl, workDir, new CloneOptions
            {
                BranchName = branch,
                IsBare = false,
            });
        }

        using var repo = new Repository(workDir);

        // Pull (fetch + fast-forward)
        var remote = repo.Network.Remotes.FirstOrDefault()
            ?? throw new InvalidOperationException($"No remote configured in notebook '{notebook.Name}'.");

        var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification).ToArray();
        Commands.Fetch(repo, remote.Name, refSpecs, new FetchOptions(), logMessage: null);
        logger.LogDebug("Fetched from remote '{Remote}' for notebook '{Name}'", remote.Name, notebook.Name);

        var remoteBranch = repo.Branches[$"{remote.Name}/{branch}"];
        if (remoteBranch != null)
        {
            var localBranch = repo.Branches[branch];
            if (localBranch == null)
            {
                localBranch = repo.CreateBranch(branch, remoteBranch.Tip);
                repo.Branches.Update(localBranch, b => b.TrackedBranch = remoteBranch.CanonicalName);
            }
            else
            {
                var divergence = repo.ObjectDatabase.CalculateHistoryDivergence(localBranch.Tip, remoteBranch.Tip);
                if (divergence.AheadBy == 0 && divergence.BehindBy > 0)
                {
                    repo.Refs.UpdateTarget(localBranch.Reference, remoteBranch.Tip.Id);
                    logger.LogDebug("Fast-forwarded '{Branch}' for notebook '{Name}'", branch, notebook.Name);
                }
            }
        }

        // Import markdown files from the working tree into DB
        ImportMarkdownFiles(notebook, workDir, db);

        // Export DB changes to files
        ExportNotesToFiles(notebook, workDir, db);

        // Stage all changes and commit+push if anything changed
        var status = repo.RetrieveStatus();
        if (!status.IsDirty) return;

        Commands.Stage(repo, "*");

        var signature = new Signature("IssuePit Notes Sync", "notes-sync@issuepit.local", DateTimeOffset.UtcNow);
        repo.Commit("sync: update notes from IssuePit", signature, signature);
        logger.LogInformation("Committed changes for notebook '{Name}'", notebook.Name);

        var pushRefSpec = $"refs/heads/{branch}:refs/heads/{branch}";
        repo.Network.Push(remote, pushRefSpec, new PushOptions());
        logger.LogInformation("Pushed changes for notebook '{Name}'", notebook.Name);
    }

    private void ImportMarkdownFiles(Notebook notebook, string workDir, NotesDbContext db)
    {
        var mdFiles = Directory
            .GetFiles(workDir, "*.md", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
            .ToList();

        foreach (var filePath in mdFiles)
        {
            var title = Path.GetFileNameWithoutExtension(filePath);
            var slug = GenerateSlug(title);
            var content = File.ReadAllText(filePath);
            var fileLastWrite = File.GetLastWriteTimeUtc(filePath);

            var existingNote = db.Notes
                .FirstOrDefault(n => n.NotebookId == notebook.Id && n.Slug == slug);

            if (existingNote is null)
            {
                db.Notes.Add(new NoteEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = notebook.TenantId,
                    NotebookId = notebook.Id,
                    Title = title,
                    Content = content,
                    Slug = slug,
                    Status = NoteStatus.Published,
                    Version = 1,
                    CreatedAt = fileLastWrite,
                    UpdatedAt = fileLastWrite,
                });
                logger.LogDebug("Imported note '{Title}' from file", title);
            }
            else if (fileLastWrite > existingNote.UpdatedAt)
            {
                existingNote.Content = content;
                existingNote.Title = title;
                existingNote.UpdatedAt = fileLastWrite;
                existingNote.Version++;
                logger.LogDebug("Updated note '{Title}' from file (file is newer)", title);
            }
        }
    }

    private void ExportNotesToFiles(Notebook notebook, string workDir, NotesDbContext db)
    {
        var notes = db.Notes.Where(n => n.NotebookId == notebook.Id).ToList();
        foreach (var note in notes)
        {
            var filePath = Path.Combine(workDir, $"{note.Slug}.md");
            if (File.Exists(filePath))
            {
                var fileLastWrite = File.GetLastWriteTimeUtc(filePath);
                if (note.UpdatedAt > fileLastWrite)
                {
                    File.WriteAllText(filePath, note.Content);
                    File.SetLastWriteTimeUtc(filePath, note.UpdatedAt);
                    logger.LogDebug("Exported note '{Title}' to file (DB is newer)", note.Title);
                }
            }
            else
            {
                File.WriteAllText(filePath, note.Content);
                File.SetLastWriteTimeUtc(filePath, note.UpdatedAt);
                logger.LogDebug("Created file for note '{Title}'", note.Title);
            }
        }
    }

    private static string GetWorkDir(Notebook notebook)
    {
        var baseDir = Environment.GetEnvironmentVariable("GitSync__WorkDir")
                      ?? Path.Combine(Path.GetTempPath(), "issuepit-notes-git");
        var dir = Path.Combine(baseDir, notebook.Id.ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Generates a URL/filename-safe slug from a note title.
    /// Collapses whitespace, removes non-alphanumeric characters, lowercases.
    /// </summary>
    private static string GenerateSlug(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "untitled";

        // Normalise unicode and lowercase
        var slug = title.Normalize(System.Text.NormalizationForm.FormD)
            .ToLowerInvariant();

        // Replace whitespace sequences with a single dash
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");

        // Keep only alphanumeric, dashes, and dots
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-\.]", "");

        // Collapse consecutive dashes
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-{2,}", "-");

        return slug.Trim('-');
    }
}
