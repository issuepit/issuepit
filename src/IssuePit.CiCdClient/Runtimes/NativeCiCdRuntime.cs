using System.Diagnostics;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.CiCdClient.Runtimes;

/// <summary>
/// Runs <c>act</c> directly on the host machine as a child process.
///
/// Reads from:
/// <list type="bullet">
///   <item><c>CiCd__ActBinaryPath</c> — path to the <c>act</c> binary (default: <c>act</c>)</item>
///   <item><c>CiCd__DefaultWorkspacePath</c> — fallback workspace directory</item>
/// </list>
/// </summary>
public class NativeCiCdRuntime(ILogger<NativeCiCdRuntime> logger, IConfiguration configuration) : ICiCdRuntime
{
    public async Task RunAsync(
        CiCdRun run,
        TriggerPayload trigger,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var actBin = configuration["CiCd:ActBinaryPath"] ?? "act";
        var workspacePath = trigger.WorkspacePath ?? configuration["CiCd:DefaultWorkspacePath"];

        if (string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath))
            throw new InvalidOperationException(
                $"Workspace path '{workspacePath}' is not configured or does not exist. " +
                "Set CiCd__DefaultWorkspacePath to the repository workspace.");

        // Validate the workflow with actionlint before running act (best-effort — silently skipped if not installed).
        await TryRunActionlintAsync(workspacePath, trigger.Workflow, onLogLine, cancellationToken);

        // Build argument list: base args + -P platform flags to suppress act's interactive
        // image-selection prompt in non-TTY environments.
        var actRunnerImage = !string.IsNullOrWhiteSpace(trigger.ActRunnerImage)
            ? trigger.ActRunnerImage
            : configuration["CiCd:ActImage"] ?? "catthehacker/ubuntu:act-latest";
        var platformLabels = new[] { "ubuntu-latest", "ubuntu-24.04", "ubuntu-22.04", "ubuntu-20.04" };

        var argsList = BuildActArgumentsList(trigger).ToList();
        foreach (var label in platformLabels)
        {
            argsList.Add("-P");
            argsList.Add($"{label}={actRunnerImage}");
        }

        logger.LogInformation("Running act (native) for run {RunId}: {ActBin} {Args}", run.Id, actBin, string.Join(' ', argsList));

        var psi = new ProcessStartInfo
        {
            FileName = actBin,
            WorkingDirectory = workspacePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in argsList)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();

        try
        {
            var stdoutTask = StreamOutputAsync(process.StandardOutput, LogStream.Stdout, onLogLine, cancellationToken);
            var stderrTask = StreamOutputAsync(process.StandardError, LogStream.Stderr, onLogLine, cancellationToken);

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Kill the process when cancellation (user cancel or app shutdown) is requested.
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw;
        }

        if (process.ExitCode != 0)
            throw new Exception(
                $"act exited with code {process.ExitCode} " +
                $"(workspace: {workspacePath}, event: {trigger.EventName ?? "push"}, workflow: {trigger.Workflow ?? "default"})");
    }

    /// <summary>
    /// Tries to run <c>actionlint</c> on the workflow file before starting <c>act</c>.
    /// Results are emitted as log lines. Best-effort: silently skipped when actionlint is not installed
    /// or the workflow file cannot be found. Never throws.
    /// </summary>
    internal static async Task TryRunActionlintAsync(
        string workspacePath,
        string? workflow,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        try
        {
            string? workflowPath = null;
            if (!string.IsNullOrWhiteSpace(workflow))
            {
                var wfFileName = Path.GetFileName(workflow);
                var candidate1 = Path.Combine(workspacePath, ".github", "workflows", wfFileName);
                var candidate2 = Path.Combine(workspacePath, workflow.TrimStart('/').TrimStart('\\').Replace('/', Path.DirectorySeparatorChar));
                workflowPath = File.Exists(candidate1) ? candidate1 : (File.Exists(candidate2) ? candidate2 : null);
            }

            if (workflowPath is null)
                return;

            // Use ArgumentList to avoid any argument-splitting or injection risks from paths with spaces.
            var psi = new ProcessStartInfo("actionlint")
            {
                WorkingDirectory = workspacePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-color=false");
            psi.ArgumentList.Add(workflowPath);

            using var process = new Process { StartInfo = psi };
            if (!process.Start())
            {
                await onLogLine("[ACTIONLINT] Could not start actionlint process.", LogStream.Stdout);
                return;
            }

            await onLogLine("[ACTIONLINT] Validating workflow...", LogStream.Stdout);

            var stdoutTask = StreamOutputAsync(process.StandardOutput, LogStream.Stdout, onLogLine, cancellationToken);
            var stderrTask = StreamOutputAsync(process.StandardError, LogStream.Stderr, onLogLine, cancellationToken);

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            // actionlint binary not found on PATH — silently skip without polluting the log.
        }
        catch (Exception ex)
        {
            // Unexpected error (e.g. I/O failure) — emit a debug note but don't abort the run.
            await onLogLine($"[ACTIONLINT] Skipped: {ex.Message}", LogStream.Stdout);
        }
    }

    private static async Task StreamOutputAsync(
        StreamReader reader,
        LogStream stream,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await onLogLine(line, stream);
        }
    }

    internal static string BuildActArguments(TriggerPayload trigger) =>
        string.Join(' ', BuildActArgumentsList(trigger));

    /// <summary>Default number of concurrent jobs passed to <c>--concurrent-jobs</c> when neither project nor org specifies a value.</summary>
    internal const int DefaultConcurrentJobs = 4;

    internal static IReadOnlyList<string> BuildActArgumentsList(TriggerPayload trigger)
    {
        var list = new List<string> { trigger.EventName ?? "push" };

        // Remove runner containers after each job completes (same as docker run --rm).
        list.Add("--rm");

        // Output logs as JSON so the worker can extract job/step info for the UI.
        list.Add("--json");

        if (!string.IsNullOrWhiteSpace(trigger.Workflow))
        {
            list.Add("-W");
            // Normalize bare filenames (e.g. "ci.yml") to ".github/workflows/ci.yml" so that
            // act resolves the file relative to the workspace root instead of failing with
            // "no such file or directory" when looking for /workspace/ci.yml.
            var workflow = trigger.Workflow;
            if (string.IsNullOrEmpty(Path.GetDirectoryName(workflow)))
                workflow = Path.Combine(".github", "workflows", workflow);
            list.Add(workflow);
        }

        foreach (var pair in ParseKeyValuePairs(trigger.ActEnv))
        {
            list.Add("--env");
            list.Add(pair);
        }

        foreach (var pair in ParseKeyValuePairs(trigger.ActSecrets))
        {
            list.Add("--secret");
            list.Add(pair);
        }

        // Enable act's built-in artifact server so actions/upload-artifact and
        // actions/download-artifact work without a real GitHub token.
        if (!string.IsNullOrWhiteSpace(trigger.ArtifactServerPath))
        {
            list.Add("--artifact-server-path");
            list.Add(trigger.ArtifactServerPath);
        }
        if (trigger.Inputs is not null)
        {
            foreach (var kv in trigger.Inputs)
            {
                list.Add("--input");
                list.Add($"{kv.Key}={kv.Value}");
            }
        }

        // Limit concurrent act jobs. Default is 4; 0 means unlimited (no flag).
        var concurrentJobs = trigger.ConcurrentJobs ?? DefaultConcurrentJobs;
        if (concurrentJobs > 0)
        {
            list.Add("--concurrent-jobs");
            list.Add(concurrentJobs.ToString());
        }

        // Action/repo cache support (act --action-cache-path, --use-new-action-cache, --action-offline-mode).
        if (!string.IsNullOrWhiteSpace(trigger.ActionCachePath))
        {
            list.Add("--action-cache-path");
            list.Add(trigger.ActionCachePath);
        }

        if (trigger.UseNewActionCache == true)
            list.Add("--use-new-action-cache");

        if (trigger.ActionOfflineMode == true)
            list.Add("--action-offline-mode");

        // Local repository rerouting: map remote owner/repo@ref to local path for private workflows/actions.
        foreach (var mapping in ParseKeyValuePairs(trigger.LocalRepositories))
        {
            list.Add("--local-repository");
            list.Add(mapping);
        }

        return list;
    }

    /// <summary>
    /// Parses a newline-separated list of KEY=VALUE pairs, skipping blank lines and lines
    /// where the key part (before the first '=') is empty.
    /// </summary>
    internal static IEnumerable<string> ParseKeyValuePairs(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            yield break;

        foreach (var line in input.Split('\n'))
        {
            var trimmed = line.Trim();
            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx > 0)
                yield return trimmed;
        }
    }
}
