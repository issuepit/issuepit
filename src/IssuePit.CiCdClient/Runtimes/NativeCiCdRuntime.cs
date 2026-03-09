using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
    /// <summary>Maximum number of times to attempt running act before giving up.</summary>
    private const int MaxActAttempts = 2;

    /// <summary>
    /// Seconds to wait between act retry attempts. Used when act exits with no jobs having run,
    /// which typically indicates a Docker container name collision due to async --rm cleanup.
    /// </summary>
    private const int ActRetryDelaySeconds = 5;
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
        // Print act version for diagnostics before the actual run.
        await TryLogActVersionAsync(actBin, onLogLine, cancellationToken);

        var args = BuildActArguments(trigger);

        var argsList = BuildActArgumentsList(trigger).ToList();
        foreach (var label in platformLabels)
        {
            argsList.Add("-P");
            argsList.Add($"{label}={actRunnerImage}");
        }

        // Each run must use its own artifact-server port so that consecutive runs do not
        // collide when act's default port (34567) is briefly in TCP TIME_WAIT after a prior run.
        // We probe for a free port and pass it explicitly; the slight TOCTOU gap is acceptable.
        if (!string.IsNullOrWhiteSpace(trigger.ArtifactServerPath))
        {
            argsList.Add("--artifact-server-port");
            argsList.Add(FindFreePort().ToString());
        }

        logger.LogInformation("Running act (native) for run {RunId}: {ActBin} {Args}", run.Id, actBin, string.Join(' ', argsList));

        // Act derives Docker container names from the workflow and job names (not the workspace
        // path). Consecutive runs against the same workflow therefore get the same container
        // names. When Docker's async --rm cleanup from the previous run is still in progress the
        // next run's "docker create" call fails with "container name already in use". The git
        // worktree below does NOT change the container name hash (it is workspace-path
        // independent), but it still provides a clean, isolated working directory per run.
        var originalWorkspacePath = workspacePath;
        var tempWorktree = Path.Combine(Path.GetTempPath(), $"act-{run.Id:N}");
        var worktreeCreated = false;
        try
        {
            var psiWorktree = new ProcessStartInfo("git")
            {
                WorkingDirectory = workspacePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psiWorktree.ArgumentList.Add("worktree");
            psiWorktree.ArgumentList.Add("add");
            psiWorktree.ArgumentList.Add("--detach");
            psiWorktree.ArgumentList.Add(tempWorktree);
            using var gitAddProcess = new Process { StartInfo = psiWorktree };
            gitAddProcess.Start();
            var gitAddStderr = await gitAddProcess.StandardError.ReadToEndAsync(cancellationToken);
            await gitAddProcess.WaitForExitAsync(cancellationToken);
            if (gitAddProcess.ExitCode == 0)
            {
                worktreeCreated = true;
                workspacePath = tempWorktree;
                logger.LogDebug("Created git worktree at {Worktree} for run {RunId}", tempWorktree, run.Id);
            }
            else
            {
                logger.LogWarning(
                    "git worktree add failed (exit {ExitCode}) for run {RunId}; proceeding with original workspace (container names may collide). stderr: {Stderr}",
                    gitAddProcess.ExitCode, run.Id, gitAddStderr.Trim());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not create git worktree for run {RunId}; proceeding with original workspace", run.Id);
        }

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

        // Act's container names are derived from the workflow name + job name and are therefore
        // identical across consecutive runs of the same workflow. Docker's async --rm cleanup from
        // the previous run can still be in-progress when the next run tries to create containers
        // with the same names ("container name already in use"). In that case act exits non-zero
        // without running any jobs. Retry once after a brief pause so Docker can finish cleanup.
        Exception? actException = null;
        for (var attempt = 1; attempt <= MaxActAttempts; attempt++)
        {
            var anyJobFailed = false;
            var anyJobSucceeded = false;
            Func<string, LogStream, Task> trackingLogLine = async (line, stream) =>
            {
                if (line.Length > 0 && line[0] == '{')
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("msg", out var msgEl))
                        {
                            var msg = msgEl.GetString();
                            // act v0.2.84 emits "🏁  Job succeeded" / "🏁  Job failed" (emoji prefix);
                            // use EndsWith so the check works regardless of any leading characters.
                            if (msg?.EndsWith("Job succeeded", StringComparison.Ordinal) == true) anyJobSucceeded = true;
                            else if (msg?.EndsWith("Job failed", StringComparison.Ordinal) == true) anyJobFailed = true;
                        }
                    }
                    catch { /* non-JSON line — ignore */ }
                }
                await onLogLine(line, stream);
            };

            using (var process = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                process.Start();

                try
                {
                    var stdoutTask = StreamOutputAsync(process.StandardOutput, LogStream.Stdout, trackingLogLine, cancellationToken);
                    var stderrTask = StreamOutputAsync(process.StandardError, LogStream.Stderr, trackingLogLine, cancellationToken);

                    await Task.WhenAll(stdoutTask, stderrTask);
                    await process.WaitForExitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Kill the process when cancellation (user cancel or app shutdown) is requested.
                    try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                    throw;
                }

                if (process.ExitCode == 0)
                {
                    actException = null;
                    break;
                }

                if (anyJobFailed)
                {
                    // At least one job explicitly reported failure — real workflow failure, do not retry.
                    actException = new Exception(
                        $"act exited with code {process.ExitCode} " +
                        $"(workspace: {workspacePath}, event: {trigger.EventName ?? "push"}, workflow: {trigger.Workflow ?? "default"})");
                    break;
                }

                if (anyJobSucceeded)
                {
                    // All jobs reported success; non-zero exit is from a step failure handled by
                    // continue-on-error. Treat the run as succeeded.
                    logger.LogWarning(
                        "act exited with code {ExitCode} for run {RunId} but all jobs succeeded " +
                        "(likely a step failure with continue-on-error); treating run as succeeded",
                        process.ExitCode, run.Id);
                    actException = null;
                    break;
                }

                // No jobs ran at all — likely a Docker container name collision from async --rm
                // cleanup of the previous run. Build the exception message for potential re-throw.
                actException = new Exception(
                    $"act exited with code {process.ExitCode} and no jobs ran " +
                    $"(workspace: {workspacePath}, event: {trigger.EventName ?? "push"}, workflow: {trigger.Workflow ?? "default"})");

                if (attempt < MaxActAttempts)
                {
                    logger.LogWarning(
                        "act exited with code {ExitCode} and no jobs ran for run {RunId} " +
                        "(attempt {Attempt}/{MaxAttempts}); waiting for Docker cleanup before retry",
                        process.ExitCode, run.Id, attempt, MaxActAttempts);
                    await Task.Delay(TimeSpan.FromSeconds(ActRetryDelaySeconds), cancellationToken);
                }
            }
        }

        if (worktreeCreated)
        {
            try
            {
                var psiRemove = new ProcessStartInfo("git")
                {
                    WorkingDirectory = originalWorkspacePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                psiRemove.ArgumentList.Add("worktree");
                psiRemove.ArgumentList.Add("remove");
                psiRemove.ArgumentList.Add("--force");
                psiRemove.ArgumentList.Add(tempWorktree);
                using var gitRemoveProcess = new Process { StartInfo = psiRemove };
                gitRemoveProcess.Start();
                var gitRemoveStderr = await gitRemoveProcess.StandardError.ReadToEndAsync(CancellationToken.None);
                await gitRemoveProcess.WaitForExitAsync(CancellationToken.None);
                if (gitRemoveProcess.ExitCode != 0)
                    logger.LogWarning(
                        "git worktree remove exited with {ExitCode} for {Worktree}. stderr: {Stderr}",
                        gitRemoveProcess.ExitCode, tempWorktree, gitRemoveStderr.Trim());
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to remove git worktree {Worktree}", tempWorktree);
                // Best-effort cleanup — do not mask act exception
            }
        }

        if (actException is not null)
            throw actException;
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

    /// <summary>
    /// Runs <c>act --version</c> and emits the output as a <c>[DEBUG]</c> log line.
    /// Best-effort: silently skipped when act is not found or fails. Never throws.
    /// </summary>
    internal static async Task TryLogActVersionAsync(
        string actBin,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo(actBin, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = new Process { StartInfo = psi };
            if (!process.Start())
                return;

            var version = (await process.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
            await process.WaitForExitAsync(cancellationToken);

            if (!string.IsNullOrEmpty(version))
                await onLogLine($"[DEBUG] Act version    : {version}", LogStream.Stdout);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch { /* best-effort */ }
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

        // Variables are passed as --var so they are accessible via ${{ vars.KEY }} in workflow
        // expressions, including job-level if: conditions where the env context is not available.
        foreach (var pair in ParseKeyValuePairs(trigger.ActVars))
        {
            list.Add("--var");
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

    /// <summary>
    /// Probes the OS for a free TCP port by binding to port 0 (kernel assigns one) and
    /// immediately releasing it. Used to give each act run a unique artifact-server port
    /// so that consecutive runs do not collide when the default port is in TIME_WAIT.
    /// </summary>
    internal static int FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
