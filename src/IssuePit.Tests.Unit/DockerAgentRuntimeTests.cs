using System.Reflection;
using IssuePit.ExecutionClient.Runtimes;

namespace IssuePit.Tests.Unit;

/// <summary>Unit tests for <see cref="DockerAgentRuntime"/> helper methods.</summary>
[Trait("Category", "Unit")]
public class DockerAgentRuntimeTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // ToDockerHostUrl
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("http://localhost:5040", "http://host.docker.internal:5040")]
    [InlineData("http://localhost:5040/", "http://host.docker.internal:5040")]
    [InlineData("http://localhost:5040/some/path", "http://host.docker.internal:5040/some/path")]
    [InlineData("http://127.0.0.1:8080", "http://host.docker.internal:8080")]
    // Note: 443 is the default HTTPS port so UriBuilder omits it, matching standard URI normalisation.
    [InlineData("https://localhost:443", "https://host.docker.internal")]
    public void ToDockerHostUrl_LocalhostUrl_ReturnsDockerHost(string input, string expected)
    {
        var result = DockerAgentRuntime.ToDockerHostUrl(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://127.0.0.1")]
    public void ToDockerHostUrl_DefaultPortUrl_HostIsReplaced(string input)
    {
        var result = DockerAgentRuntime.ToDockerHostUrl(input);
        Assert.Contains("host.docker.internal", result);
        Assert.DoesNotContain("localhost", result);
        Assert.DoesNotContain("127.0.0.1", result);
    }

    [Theory]
    [InlineData("http://some-service:5040")]
    [InlineData("http://mcp.example.com/sse")]
    [InlineData("https://api.openai.com/v1")]
    public void ToDockerHostUrl_NonLocalhostUrl_IsUnchanged(string input)
    {
        var result = DockerAgentRuntime.ToDockerHostUrl(input);
        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToDockerHostUrl_NullOrEmpty_ReturnsInput(string? input)
    {
        var result = DockerAgentRuntime.ToDockerHostUrl(input);
        Assert.Equal(input, result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Embedded entrypoint.sh content validation
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the entrypoint.sh embedded resource from the ExecutionClient assembly and
    /// verifies it contains <c>exec "$@"</c> as the final container handoff.
    /// Without this line the container exits immediately after setup and all subsequent
    /// <c>docker exec</c> commands fail with "container is not running".
    /// </summary>
    [Fact]
    public void EntrypointSh_ContainsExecAtSign_ContainerHandoff()
    {
        var content = ReadEntrypoint();
        Assert.True(
            content.Contains("exec \"$@\""),
            "entrypoint.sh must contain 'exec \"$@\"' to hand off to the container CMD " +
            "(sleep infinity / opencode). Without it the container exits immediately.");
    }

    /// <summary>
    /// Verifies the entrypoint does NOT call <c>exit 1</c> in the dockerd startup failure
    /// path. If dockerd times out or fails to start, the container must continue running
    /// (with a warning) so the agent can still operate.
    /// </summary>
    [Fact]
    public void EntrypointSh_DockerdFailure_DoesNotCallExit1()
    {
        var content = ReadEntrypoint();

        // The only legitimate exit 1 in the entrypoint is inside the git push wrapper
        // (blocks agent tools from pushing). The dockerd failure block must NOT exit 1
        // — it should only log a warning and continue.
        //
        // We verify this by checking that the dockerd failure message is followed by a
        // non-fatal warning pattern rather than `exit 1`.
        var dockerdWarningIdx = content.IndexOf("dockerd did not start", StringComparison.Ordinal);
        Assert.True(dockerdWarningIdx >= 0,
            "entrypoint.sh must contain a warning message when dockerd fails to start instead of exiting.");

        // Find the next `exit 1` after the dockerd failure message.
        var exitAfterWarning = content.IndexOf("exit 1", dockerdWarningIdx, StringComparison.Ordinal);
        // The next `exit 1` (if any) must be from the git push wrapper, which comes BEFORE dockerd setup.
        // So any `exit 1` that appears after the dockerd warning is a bug.
        Assert.True(exitAfterWarning < 0,
            "entrypoint.sh must not call 'exit 1' after the dockerd failure warning. " +
            "The container should continue running without DinD.");
    }

    private static string ReadEntrypoint()
    {
        var assembly = Assembly.GetAssembly(typeof(DockerAgentRuntime))
            ?? throw new InvalidOperationException("Could not load ExecutionClient assembly.");
        using var stream = assembly.GetManifestResourceStream("entrypoint.sh")
            ?? throw new InvalidOperationException(
                "Embedded resource 'entrypoint.sh' not found. Ensure it is included as EmbeddedResource in the csproj.");
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }
}
