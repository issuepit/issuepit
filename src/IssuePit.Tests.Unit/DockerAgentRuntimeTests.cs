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
}
