using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace IssuePit.Tests.Unit;

/// <summary>
/// Integration tests that validate the Docker exec-model container lifecycle and exec semantics
/// introduced by the act CI startup restoration. Requires a running Docker daemon.
/// Run manually with: dotnet test --filter "Category=Docker"
/// </summary>
[Trait("Category", "Docker")]
public class DockerExecModelTests : IAsyncLifetime
{
    private const string TestImage = "alpine:3";
    private DockerClient _client = null!;
    private string? _containerId;

    public async Task InitializeAsync()
    {
        _client = new DockerClientConfiguration().CreateClient();

        // Pull alpine:3 if not present
        await _client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = TestImage },
            null,
            new Progress<JSONMessage>());

        // Create container using the new TTY + OpenStdin idiom (Cmd=["/bin/sh"], Entrypoint=[], Tty=true, OpenStdin=true)
        var response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = TestImage,
            Name = $"issuepit-test-{Guid.NewGuid():N}"[..24],
            Cmd = ["/bin/sh"],
            Entrypoint = [],
            Tty = true,
            OpenStdin = true,
        });
        _containerId = response.ID;

        await _client.Containers.StartContainerAsync(_containerId, new ContainerStartParameters());
    }

    public async Task DisposeAsync()
    {
        if (_containerId is not null)
        {
            try
            {
                await _client.Containers.RemoveContainerAsync(
                    _containerId, new ContainerRemoveParameters { Force = true });
            }
            catch { /* best-effort */ }
        }
        _client.Dispose();
    }

    [Fact]
    public async Task ContainerCreateAndStart_WithTtyAndOpenStdin_IsRunning()
    {
        var inspect = await _client.Containers.InspectContainerAsync(_containerId!);
        Assert.True(inspect.State.Running, "Container should be running after start with Tty=true and OpenStdin=true");
    }

    [Fact]
    public async Task ExecCommand_Echo_ReturnsExpectedOutput()
    {
        var lines = new List<string>();
        var exitCode = await ExecCommandAsync(
            _containerId!,
            ["echo", "hello-from-docker"],
            (line, _) => { lines.Add(line); return Task.CompletedTask; });

        Assert.Equal(0, exitCode);
        Assert.Contains(lines, l => l.Contains("hello-from-docker"));
    }

    [Fact]
    public async Task ExecCommand_MultiStepFileIo_ReadsWrittenContent()
    {
        // Step 1: write a file
        var writeExitCode = await ExecCommandAsync(
            _containerId!,
            ["/bin/sh", "-c", "echo testcontent > /tmp/testfile.txt"],
            (_, _) => Task.CompletedTask);
        Assert.Equal(0, writeExitCode);

        // Step 2: read the file back
        var readLines = new List<string>();
        var readExitCode = await ExecCommandAsync(
            _containerId!,
            ["cat", "/tmp/testfile.txt"],
            (line, _) => { readLines.Add(line); return Task.CompletedTask; });
        Assert.Equal(0, readExitCode);
        Assert.Contains(readLines, l => l.Contains("testcontent"));
    }

    [Fact]
    public async Task ExecCommand_NonZeroExitCode_IsPropagated()
    {
        var exitCode = await ExecCommandAsync(
            _containerId!,
            ["/bin/sh", "-c", "exit 42"],
            (_, _) => Task.CompletedTask);

        Assert.Equal(42, exitCode);
    }

    /// <summary>
    /// Executes a command in the container using the same exec semantics as DockerCiCdRuntime:
    /// Tty=true, AttachStdin=true, docker exec -it ...
    /// </summary>
    private async Task<long> ExecCommandAsync(
        string containerId,
        IList<string> cmd,
        Func<string, MultiplexedStream.TargetStream, Task> onLine)
    {
        var execCreate = await _client.Exec.ExecCreateContainerAsync(
            containerId,
            new ContainerExecCreateParameters
            {
                AttachStdin = true,
                AttachStdout = true,
                AttachStderr = true,
                Tty = true,
                Cmd = cmd,
            });

        using var stream = await _client.Exec.StartAndAttachContainerExecAsync(
            execCreate.ID, tty: true, CancellationToken.None);

        var buffer = new byte[4096];
        var remainder = string.Empty;
        while (true)
        {
            var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, CancellationToken.None);
            if (result.EOF) break;

            var text = remainder + Encoding.UTF8.GetString(buffer, 0, result.Count);
            var lineBreakPos = text.IndexOf('\n');
            while (lineBreakPos >= 0)
            {
                var line = text[..lineBreakPos].TrimEnd('\r');
                if (!string.IsNullOrEmpty(line))
                    await onLine(line, result.Target);
                text = text[(lineBreakPos + 1)..];
                lineBreakPos = text.IndexOf('\n');
            }
            remainder = text;
        }
        if (!string.IsNullOrEmpty(remainder.TrimEnd('\r')))
            await onLine(remainder.TrimEnd('\r'), MultiplexedStream.TargetStream.StandardOut);

        var inspect = await _client.Exec.InspectContainerExecAsync(execCreate.ID, CancellationToken.None);
        return inspect.ExitCode;
    }
}
