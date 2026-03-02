using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.CiCdClient.Runtimes;

/// <summary>
/// Runs <c>act</c> inside a Docker container, mounting the workspace as a volume.
/// This is the default CI/CD runtime.
///
/// Reads from:
/// <list type="bullet">
///   <item><c>CiCd__Docker__Image</c> — Docker image that has <c>act</c> installed
///     (default: <c>ghcr.io/catthehacker/ubuntu:custom-24.04</c>)</item>
///   <item><c>CiCd__ActBinaryPath</c> — path to <c>act</c> inside the container (default: <c>act</c>)</item>
///   <item><c>CiCd__DefaultWorkspacePath</c> — fallback host path to the repository workspace</item>
/// </list>
/// </summary>
public class DockerCiCdRuntime(
    ILogger<DockerCiCdRuntime> logger,
    DockerClient dockerClient,
    IConfiguration configuration) : ICiCdRuntime
{
    // Docker image used to run act. Uses the "custom" variant from ghcr.io/catthehacker/ubuntu
    // which includes dotnet and JavaScript tooling (see https://github.com/catthehacker/docker_images).
    private const string DefaultImage = "ghcr.io/catthehacker/ubuntu:custom-24.04";

    public async Task RunAsync(
        CiCdRun run,
        TriggerPayload trigger,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var image = configuration["CiCd__Docker__Image"] ?? DefaultImage;
        var actBin = configuration["CiCd__ActBinaryPath"] ?? "act";
        var workspacePath = trigger.WorkspacePath ?? configuration["CiCd__DefaultWorkspacePath"];

        if (string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath))
            throw new InvalidOperationException(
                $"Workspace path '{workspacePath}' is not configured or does not exist. " +
                "Set CiCd__DefaultWorkspacePath to the repository workspace.");

        var actArgs = NativeCiCdRuntime.BuildActArgumentsList(trigger);
        var cmd = new[] { actBin }.Concat(actArgs).ToList();

        // Emit verbose diagnostics as the first log lines so they appear in the run's log output.
        await onLogLine($"[DEBUG] Runner machine : {Environment.MachineName}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Runtime        : Docker", LogStream.Stdout);
        await onLogLine($"[DEBUG] Docker image   : {image}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Command        : {string.Join(' ', cmd)}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Mount          : {workspacePath}:/workspace", LogStream.Stdout);
        await onLogLine($"[DEBUG] Mount          : /var/run/docker.sock:/var/run/docker.sock", LogStream.Stdout);
        await onLogLine($"[DEBUG] Working dir    : /workspace", LogStream.Stdout);

        // Verify Docker daemon is reachable before attempting heavier operations.
        try
        {
            await dockerClient.System.PingAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Cannot connect to the Docker daemon. Ensure Docker is running and the socket is accessible " +
                $"(inner: {ex.Message})", ex);
        }

        logger.LogInformation("Pulling Docker image {Image} for CI/CD run {RunId}", image, run.Id);
        var pullStart = DateTime.UtcNow;
        await onLogLine($"[DEBUG] Pull started   : {pullStart:u}", LogStream.Stdout);
        await onLogLine($"[DEBUG] Pulling image  : {image}", LogStream.Stdout);
        try
        {
            await dockerClient.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = image },
                null,
                // Progress handler is required by the API but pull status is captured via container logs
                new Progress<JSONMessage>(),
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            var msg = $"Lost connection to the Docker daemon while pulling image '{image}'. " +
                "This can happen on Windows when Docker Desktop resets named-pipe connections. " +
                "Try running the CI/CD run again.";
            await onLogLine($"[ERROR] {msg}", LogStream.Stderr);
            foreach (var line in ex.ToString().Split('\n'))
                await onLogLine(line.TrimEnd('\r'), LogStream.Stderr);
            throw new InvalidOperationException(msg, ex);
        }

        var pullDuration = DateTime.UtcNow - pullStart;
        await onLogLine(
            $"[DEBUG] Pull finished  : {DateTime.UtcNow:u} (took {pullDuration.TotalSeconds:F1}s)",
            LogStream.Stdout);

        logger.LogInformation("Creating Docker container from image {Image} for CI/CD run {RunId}", image, run.Id);

        var createParams = new CreateContainerParameters
        {
            Image = image,
            Cmd = cmd,
            WorkingDir = "/workspace",
            HostConfig = new HostConfig
            {
                Binds =
                [
                    $"{workspacePath}:/workspace",
                    // Mount Docker socket so act can spin up runner containers (DinD)
                    "/var/run/docker.sock:/var/run/docker.sock",
                ],
                AutoRemove = false,
            },
            Labels = new Dictionary<string, string>
            {
                ["issuepit.run-id"] = run.Id.ToString(),
                ["issuepit.project-id"] = run.ProjectId.ToString(),
            },
        };

        var container = await dockerClient.Containers.CreateContainerAsync(createParams, cancellationToken);
        logger.LogInformation("Created Docker container {ContainerId} for CI/CD run {RunId}",
            container.ID, run.Id);
        await onLogLine($"[DEBUG] Container ID   : {container.ID[..12]}", LogStream.Stdout);

        try
        {
            await dockerClient.Containers.StartContainerAsync(
                container.ID, new ContainerStartParameters(), cancellationToken);
        }
        catch (DockerApiException ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.BadRequest &&
            ex.ResponseBody?.Contains("executable file not found") == true)
        {
            throw new InvalidOperationException(
                $"The '{actBin}' binary was not found inside the Docker container. " +
                $"Ensure the image '{image}' has 'act' installed, or override CiCd__ActBinaryPath " +
                "with the correct path to the act binary inside the container.", ex);
        }

        var succeeded = false;
        try
        {
            var logStreamTask = StreamContainerLogsAsync(container.ID, onLogLine, cancellationToken);
            var waitResponse = await dockerClient.Containers.WaitContainerAsync(container.ID, cancellationToken);
            // Drain any remaining log output before checking the exit code.
            await logStreamTask;

            if (waitResponse.StatusCode != 0)
                throw new Exception(
                    $"act exited with code {waitResponse.StatusCode} " +
                    $"(image: {image}, event: {trigger.EventName ?? "push"}, workflow: {trigger.Workflow ?? "default"})");

            succeeded = true;
        }
        catch (OperationCanceledException)
        {
            // Kill the container when cancellation is requested.
            try
            {
                await dockerClient.Containers.KillContainerAsync(
                    container.ID, new ContainerKillParameters(), CancellationToken.None);
            }
            catch { /* best-effort */ }
            throw;
        }
        finally
        {
            var keepContainer = !succeeded && trigger.KeepContainerOnFailure;
            if (keepContainer)
            {
                await onLogLine(
                    $"[DEBUG] Container kept : {container.ID[..12]} (KeepContainerOnFailure=true)" +
                    " — run `docker ps -a` to find it, `docker exec -it <id> sh` to inspect",
                    LogStream.Stdout);
                logger.LogInformation(
                    "Keeping Docker container {ContainerId} for failed CI/CD run {RunId} (KeepContainerOnFailure=true)",
                    container.ID, run.Id);
            }
            else
            {
                try
                {
                    await dockerClient.Containers.RemoveContainerAsync(
                        container.ID,
                        new ContainerRemoveParameters { Force = true },
                        CancellationToken.None);
                }
                catch { /* best-effort */ }
            }
        }
    }

    private async Task StreamContainerLogsAsync(
        string containerId,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var logsParams = new ContainerLogsParameters
        {
            Follow = true,
            ShowStdout = true,
            ShowStderr = true,
        };

        using var stream = await dockerClient.Containers.GetContainerLogsAsync(
            containerId, false, logsParams, cancellationToken);

        var buffer = new byte[81920];
        var remainder = string.Empty;
        var lastTarget = LogStream.Stdout;

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
            if (result.EOF) break;

            lastTarget = result.Target == MultiplexedStream.TargetStream.StandardError
                ? LogStream.Stderr
                : LogStream.Stdout;

            var text = remainder + Encoding.UTF8.GetString(buffer, 0, result.Count);
            var lines = text.Split('\n');

            // All but the last element are complete lines.
            for (var i = 0; i < lines.Length - 1; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (!string.IsNullOrEmpty(line))
                    await onLogLine(line, lastTarget);
            }

            // Keep the trailing (possibly incomplete) fragment for the next iteration.
            remainder = lines[^1];
        }

        // Flush any remaining content after EOF.
        var flushed = remainder.TrimEnd('\r');
        if (!string.IsNullOrEmpty(flushed))
            await onLogLine(flushed, lastTarget);
    }
}
