using System.Diagnostics;
using IssuePit.Notes.Core.Data;
using IssuePit.Notes.Core.Entities;
using IssuePit.Notes.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Notes.Api.Services;

/// <summary>
/// Background service that periodically syncs git-backed notebooks by pulling remote changes
/// and pushing local modifications. Each git-backed notebook is stored in a local working
/// directory under <see cref="GitSyncOptions.WorkDir"/>.
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

        // Startup delay
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
                await SyncNotebookAsync(notebook, db, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Git sync failed for notebook '{Name}' ({Id})", notebook.Name, notebook.Id);
            }
        }
    }

    private async Task SyncNotebookAsync(Notebook notebook, NotesDbContext db, CancellationToken ct)
    {
        var workDir = GetWorkDir(notebook);
        var branch = notebook.GitBranch ?? "main";

        // Validate git URL and branch to prevent injection
        if (string.IsNullOrWhiteSpace(notebook.GitRepoUrl) || notebook.GitRepoUrl.Contains('\n') || notebook.GitRepoUrl.Contains('\r'))
        {
            logger.LogWarning("Invalid git URL for notebook '{Name}' ({Id}), skipping sync", notebook.Name, notebook.Id);
            return;
        }

        if (branch.Contains('\n') || branch.Contains('\r') || branch.Contains(' '))
        {
            logger.LogWarning("Invalid git branch for notebook '{Name}' ({Id}), skipping sync", notebook.Name, notebook.Id);
            return;
        }

        if (!Directory.Exists(Path.Combine(workDir, ".git")))
        {
            // Clone the repository
            logger.LogInformation("Cloning git repo for notebook '{Name}' ({Id})", notebook.Name, notebook.Id);
            var cloneResult = await RunGitAsync(workDir, ["clone", "--branch", branch, "--depth", "1", notebook.GitRepoUrl, "."], ct);
            if (!cloneResult.Success)
            {
                logger.LogError("git clone failed for notebook '{Name}': {Error}", notebook.Name, cloneResult.Error);
                return;
            }
        }
        else
        {
            // Pull latest changes
            var pullResult = await RunGitAsync(workDir, ["pull", "--ff-only"], ct);
            if (!pullResult.Success)
            {
                logger.LogWarning("git pull failed for notebook '{Name}': {Error}", notebook.Name, pullResult.Error);
            }
        }

        // Import new/changed markdown files into the database
        await ImportMarkdownFilesAsync(notebook, workDir, db, ct);

        // Export notes from DB to files (for notes created/modified in the UI)
        await ExportNotesToFilesAsync(notebook, workDir, db, ct);

        // Commit and push if there are changes
        var statusResult = await RunGitAsync(workDir, ["status", "--porcelain"], ct);
        if (statusResult.Success && !string.IsNullOrWhiteSpace(statusResult.Output))
        {
            await RunGitAsync(workDir, ["add", "-A"], ct);
            await RunGitAsync(workDir, ["commit", "-m", "sync: update notes from IssuePit"], ct);
            var pushResult = await RunGitAsync(workDir, ["push"], ct);
            if (!pushResult.Success)
            {
                logger.LogWarning("git push failed for notebook '{Name}': {Error}", notebook.Name, pushResult.Error);
            }
            else
            {
                logger.LogInformation("Pushed changes for notebook '{Name}'", notebook.Name);
            }
        }
    }

    private async Task ImportMarkdownFilesAsync(Notebook notebook, string workDir, NotesDbContext db, CancellationToken ct)
    {
        var mdFiles = Directory.GetFiles(workDir, "*.md", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.Combine(workDir, ".git")))
            .ToList();

        foreach (var filePath in mdFiles)
        {
            var relativePath = Path.GetRelativePath(workDir, filePath);
            var title = Path.GetFileNameWithoutExtension(relativePath);
            var slug = title.ToLowerInvariant().Replace(' ', '-');
            var content = await File.ReadAllTextAsync(filePath, ct);
            var fileLastWrite = File.GetLastWriteTimeUtc(filePath);

            var existingNote = await db.Notes
                .FirstOrDefaultAsync(n => n.NotebookId == notebook.Id && n.Slug == slug, ct);

            if (existingNote is null)
            {
                // Import new note
                var note = new Note
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
                };
                db.Notes.Add(note);
                logger.LogDebug("Imported note '{Title}' from file for notebook '{Notebook}'", title, notebook.Name);
            }
            else if (fileLastWrite > existingNote.UpdatedAt)
            {
                // Update from file (file is newer than DB)
                existingNote.Content = content;
                existingNote.Title = title;
                existingNote.UpdatedAt = fileLastWrite;
                existingNote.Version++;
                logger.LogDebug("Updated note '{Title}' from file for notebook '{Notebook}'", title, notebook.Name);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ExportNotesToFilesAsync(Notebook notebook, string workDir, NotesDbContext db, CancellationToken ct)
    {
        var notes = await db.Notes
            .Where(n => n.NotebookId == notebook.Id)
            .ToListAsync(ct);

        foreach (var note in notes)
        {
            var fileName = $"{note.Slug}.md";
            var filePath = Path.Combine(workDir, fileName);

            if (File.Exists(filePath))
            {
                var fileLastWrite = File.GetLastWriteTimeUtc(filePath);
                if (note.UpdatedAt > fileLastWrite)
                {
                    // DB is newer — export to file
                    await File.WriteAllTextAsync(filePath, note.Content, ct);
                    File.SetLastWriteTimeUtc(filePath, note.UpdatedAt);
                    logger.LogDebug("Exported note '{Title}' to file for notebook '{Notebook}'", note.Title, notebook.Name);
                }
            }
            else
            {
                // New note — create file
                await File.WriteAllTextAsync(filePath, note.Content, ct);
                File.SetLastWriteTimeUtc(filePath, note.UpdatedAt);
                logger.LogDebug("Created file for note '{Title}' in notebook '{Notebook}'", note.Title, notebook.Name);
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

    private static async Task<GitResult> RunGitAsync(string workDir, string[] arguments, CancellationToken ct)
    {
        Directory.CreateDirectory(workDir);
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        var process = Process.Start(psi);
        if (process is null)
            throw new InvalidOperationException("Failed to start git process. Ensure git is installed and available in PATH.");

        using (process)
        {
            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return new GitResult(process.ExitCode == 0, stdout.Trim(), stderr.Trim());
        }
    }

    private record GitResult(bool Success, string Output, string Error);
}
