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

        // Append a short suffix derived from the run ID so that act job containers created on the
        // host Docker daemon are identifiable by run and never collide across parallel runs
        // (issuepit/act --container-name-suffix). Using a suffix preserves the "act-" name prefix
        // that stale-container cleanup filters rely on.
        // The suffix contains only a hyphen and hex digits (UUID chars), no shell escaping needed.
        var containerNameSuffix = "-" + $"{run.Id:N}"[..ContainerNameSuffixLength];
        var argsList = BuildActArgumentsList(trigger).ToList();
        argsList.Add("--container-name-suffix");
        argsList.Add(containerNameSuffix);
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
            // NOTE: anyJobFailed, anyJobSucceeded, and containerCollisionDetected are written from
            // the trackingLogLine callback which may be invoked concurrently (stdout and stderr are
            // read in parallel). Use int + Interlocked to avoid torn reads/writes.
            var anyJobFailed = 0;
            var anyJobSucceeded = 0;
            // Tracks whether act's output contained a Docker "container name already in use" error.
            // When Docker's async --rm cleanup is still in progress from a prior run, act cannot
            // create containers with the same names and reports the job as failed. We must detect
            // this infrastructure error specifically so we can retry even when anyJobFailed=1
            // (act emits "Job failed" for container-creation failures, not just workflow failures).
            var containerCollisionDetected = 0;

            Func<string, LogStream, Task> trackingLogLine = async (line, stream) =>
            {
                // Detect Docker "container name already in use" collision errors.
                // Docker daemon returns: "Conflict. The container name ... is already in use by container..."
                // Act may also surface this in shorter forms. Check two patterns to catch both:
                //   1. Full Docker message: "container name" + "already in use"
                //   2. Shorter form:        "Conflict"       + "already in use"
                if (line.Contains("already in use", StringComparison.OrdinalIgnoreCase) &&
                    (line.Contains("container name", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Conflict", StringComparison.OrdinalIgnoreCase)))
                {
                    Interlocked.Exchange(ref containerCollisionDetected, 1);
                }

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
                            if (msg?.EndsWith("Job succeeded", StringComparison.Ordinal) == true)
                                Interlocked.Exchange(ref anyJobSucceeded, 1);
                            else if (msg?.EndsWith("Job failed", StringComparison.Ordinal) == true)
                                Interlocked.Exchange(ref anyJobFailed, 1);
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

                var isContainerCollision = containerCollisionDetected != 0;
                var isMixedOutcome = anyJobFailed != 0 && anyJobSucceeded != 0;

                // Docker container name collision is an infrastructure error — always retry
                // regardless of anyJobFailed/anyJobSucceeded, because act reports the job as
                // "failed" when it cannot create a container (same signal as a real workflow failure).
                if (!isContainerCollision)
                {
                    if (anyJobFailed != 0 && anyJobSucceeded == 0)
                    {
                        // Only job failures, no successes — real workflow failure, do not retry.
                        actException = new Exception(
                            $"act exited with code {process.ExitCode} " +
                            $"(workspace: {workspacePath}, event: {trigger.EventName ?? "push"}, workflow: {trigger.Workflow ?? "default"})");
                        break;
                    }

                    if (anyJobFailed == 0 && anyJobSucceeded != 0)
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

                    // isMixedOutcome: earlier jobs succeeded but a later job failed. This is consistent
                    // with a partial Docker container collision where a later job's container is still
                    // being removed from a prior run. Fall through to the retry path so Docker has time
                    // to finish cleanup. On the final attempt this will also fall through, set
                    // actException, and exit the loop.
                }

                // Docker container collision, mixed outcome (partial collision), or no jobs ran —
                // build the exception message for potential re-throw and retry if possible.
                var reason = isContainerCollision
                    ? "Docker container name collision"
                    : isMixedOutcome
                        ? "partial Docker container collision (some jobs succeeded, later job failed)"
                        : "no jobs ran";
                actException = new Exception(
                    $"act exited with code {process.ExitCode} ({reason}) " +
                    $"(workspace: {workspacePath}, event: {trigger.EventName ?? "push"}, workflow: {trigger.Workflow ?? "default"})");

                if (attempt < MaxActAttempts)
                {
                    logger.LogWarning(
                        "act exited with code {ExitCode} for run {RunId} ({Reason}) " +
                        "(attempt {Attempt}/{MaxAttempts}); waiting for Docker cleanup before retry",
                        process.ExitCode, run.Id, reason, attempt, MaxActAttempts);
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
    /// Verifies the <c>act</c> binary is functional by running <c>act --help</c> and emits the result as a <c>[DEBUG]</c> log line.
    /// Best-effort: silently skipped when act is not found or fails. Never throws.
    /// </summary>
    internal static async Task TryLogActVersionAsync(
        string actBin,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo(actBin, "--help")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = new Process { StartInfo = psi };
            if (!process.Start())
                return;

            // Drain output to avoid blocking; we only care about the exit code.
            _ = process.StandardOutput.ReadToEndAsync(cancellationToken);
            _ = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var status = process.ExitCode == 0 ? "act binary OK" : $"act binary check failed (exit {process.ExitCode})";
            await onLogLine($"[DEBUG] Act binary     : {status}", LogStream.Stdout);
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

    /// <summary>Default number of concurrent jobs passed to <c>--concurrent-jobs</c> when neither project nor org specifies a value.</summary>
    internal const int DefaultConcurrentJobs = 4;

    /// <summary>
    /// Length (in hex chars) used when truncating the run UUID for the <c>--container-name-suffix</c> flag.
    /// Produces a suffix like "-3f2a91b0c4" — enough uniqueness while keeping container names
    /// well below Docker's 63-character limit.
    /// </summary>
    internal const int ContainerNameSuffixLength = 10;

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

        // Skip steps: skip specific steps by name or job:step pair.
        foreach (var step in ParseLines(trigger.SkipSteps))
        {
            list.Add("--skip-step");
            list.Add(step);
        }

        return list;
    }

    /// <summary>
    /// Parses a newline-separated list of KEY=VALUE pairs, skipping blank lines and lines
    /// where the key part (before the first '=') is empty.
    /// </summary>
    internal static IEnumerable<string> ParseKeyValuePairs(string? input) =>
        ParseLines(input).Where(l => l.IndexOf('=') > 0);

    /// <summary>
    /// Parses a newline-separated list of values, skipping blank lines.
    /// </summary>
    internal static IEnumerable<string> ParseLines(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            yield break;

        foreach (var line in input.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
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
