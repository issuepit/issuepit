using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace IssuePit.Tests.Integration;

/// <summary>
/// Manually-triggered integration tests that validate the Docker exec-it flow used by
/// <c>DockerCiCdRuntime</c> against real containers.
///
/// Requires a running Docker daemon.  Run with:
///   dotnet test --filter "Category=Docker"
/// </summary>
[Trait("Category", "Docker")]
public class DockerExecIntegrationTests : IAsyncLifetime
{
    private const string TestImage = "alpine:3";

    private DockerClient _client = null!;
    private string? _containerId;

    public async Task InitializeAsync()
    {
        _client = new DockerClientConfiguration().CreateClient();

        // Pull the image once before any test in this fixture runs.
        await _client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = TestImage },
            null,
            new Progress<JSONMessage>(),
            CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        if (_containerId is not null)
        {
            try
            {
                await _client.Containers.RemoveContainerAsync(
                    _containerId,
                    new ContainerRemoveParameters { Force = true },
                    CancellationToken.None);
            }
            catch { /* best-effort */ }
        }

        _client.Dispose();
    }

    /// <summary>
    /// Verifies that a container can be created with the exec-it approach (Tty=true,
    /// OpenStdin=true, /bin/sh as main process), started successfully, and that a
    /// command exec'd into it produces the expected output.
    /// </summary>
    [Fact]
    public async Task ExecIt_EchoCommand_ReturnsExpectedOutput()
    {
        // Arrange — create container using the same settings as DockerCiCdRuntime exec model.
        var container = await _client.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = TestImage,
                Cmd = ["/bin/sh"],
                Entrypoint = [],       // clear image entrypoint
                Tty = true,            // -t
                OpenStdin = true,      // -i
                WorkingDir = "/",
                HostConfig = new HostConfig { AutoRemove = false },
            },
            CancellationToken.None);

        _containerId = container.ID;

        await _client.Containers.StartContainerAsync(
            container.ID, new ContainerStartParameters(), CancellationToken.None);

        // Act — exec "echo hello" into the running container (docker exec -it equivalent).
        var execCreate = await _client.Exec.CreateContainerExecAsync(
            container.ID,
            new ContainerExecCreateParameters
            {
                AttachStdout = true,
                AttachStderr = true,
                AttachStdin = true,
                TTY = true,
                Cmd = ["echo", "hello"],
                WorkingDir = "/",
            },
            CancellationToken.None);

        using var stream = await _client.Exec.StartContainerExecAsync(
            execCreate.ID,
            new ContainerExecStartParameters { TTY = true },
            CancellationToken.None);

        var output = await ReadStreamAsync(stream, CancellationToken.None);

        var inspect = await _client.Exec.InspectContainerExecAsync(
            execCreate.ID, CancellationToken.None);

        // Assert
        Assert.NotNull(inspect.ExitCode);
        Assert.Equal(0, inspect.ExitCode);
        Assert.Contains("hello", output);
    }

    /// <summary>
    /// Verifies that a multi-step exec flow (as used by DockerCiCdRuntime) succeeds:
    /// create container → start → exec step 1 → exec step 2 → exit codes both 0.
    /// </summary>
    [Fact]
    public async Task ExecIt_MultiStep_AllStepsSucceed()
    {
        // Arrange
        var container = await _client.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = TestImage,
                Cmd = ["/bin/sh"],
                Entrypoint = [],
                Tty = true,
                OpenStdin = true,
                WorkingDir = "/tmp",
                HostConfig = new HostConfig { AutoRemove = false },
            },
            CancellationToken.None);

        _containerId = container.ID;

        await _client.Containers.StartContainerAsync(
            container.ID, new ContainerStartParameters(), CancellationToken.None);

        // Step 1 — write a file
        var step1ExitCode = await ExecCommandAsync(
            container.ID, ["sh", "-c", "echo step1 > /tmp/result.txt"]);

        // Step 2 — read the file and verify content
        var (step2Output, step2ExitCode) = await ExecCommandWithOutputAsync(
            container.ID, ["cat", "/tmp/result.txt"]);

        // Assert
        Assert.Equal(0, step1ExitCode);
        Assert.Equal(0, step2ExitCode);
        Assert.Contains("step1", step2Output);
    }

    /// <summary>
    /// Verifies that a non-zero exit code is correctly detected via InspectContainerExecAsync.
    /// </summary>
    [Fact]
    public async Task ExecIt_FailingCommand_ReturnsNonZeroExitCode()
    {
        var container = await _client.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = TestImage,
                Cmd = ["/bin/sh"],
                Entrypoint = [],
                Tty = true,
                OpenStdin = true,
                WorkingDir = "/",
                HostConfig = new HostConfig { AutoRemove = false },
            },
            CancellationToken.None);

        _containerId = container.ID;

        await _client.Containers.StartContainerAsync(
            container.ID, new ContainerStartParameters(), CancellationToken.None);

        var exitCode = await ExecCommandAsync(
            container.ID, ["sh", "-c", "exit 42"]);

        Assert.Equal(42, exitCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────

    private async Task<long> ExecCommandAsync(string containerId, IList<string> cmd)
    {
        var (_, exitCode) = await ExecCommandWithOutputAsync(containerId, cmd);
        return exitCode;
    }

    private async Task<(string output, long exitCode)> ExecCommandWithOutputAsync(
        string containerId, IList<string> cmd)
    {
        var execCreate = await _client.Exec.CreateContainerExecAsync(
            containerId,
            new ContainerExecCreateParameters
            {
                AttachStdout = true,
                AttachStderr = true,
                AttachStdin = true,
                TTY = true,
                Cmd = cmd,
                WorkingDir = "/",
            },
            CancellationToken.None);

        using var stream = await _client.Exec.StartContainerExecAsync(
            execCreate.ID,
            new ContainerExecStartParameters { TTY = true },
            CancellationToken.None);

        var output = await ReadStreamAsync(stream, CancellationToken.None);

        var inspect = await _client.Exec.InspectContainerExecAsync(
            execCreate.ID, CancellationToken.None);

        return (output, inspect.ExitCode ?? throw new InvalidOperationException("Docker exec ExitCode was null after command completed."));
    }

    private static async Task<string> ReadStreamAsync(
        MultiplexedStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var sb = new StringBuilder();
        while (true)
        {
            var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
            if (result.EOF) break;
            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
        }
        return sb.ToString();
    }
}
