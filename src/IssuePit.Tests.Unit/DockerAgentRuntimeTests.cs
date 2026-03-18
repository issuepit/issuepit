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

    /// <summary>
    /// Verifies that <c>exec "$@"</c> is the last non-empty line of entrypoint.sh.
    /// Adding code after <c>exec "$@"</c> is harmless but accidental removal of the line
    /// (e.g., by a future edit that truncates the file) would break all container starts.
    /// This test guards against that regression.
    /// </summary>
    [Fact]
    public void EntrypointSh_ExecAtSign_IsLastNonEmptyLine()
    {
        var content = ReadEntrypoint();

        // Trim trailing whitespace and find the last non-empty line.
        var lines = content.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .ToArray();

        var lastNonEmpty = lines.LastOrDefault(l => !string.IsNullOrWhiteSpace(l));

        Assert.True(
            lastNonEmpty == "exec \"$@\"",
            $"exec \"$@\" must be the last non-empty line in entrypoint.sh to ensure the " +
            $"container CMD (sleep infinity / opencode) is started after setup. " +
            $"Actual last non-empty line: '{lastNonEmpty}'");
    }

    /// <summary>
    /// Verifies the embedded entrypoint.sh contains no CR (\r) characters.
    /// A CRLF shebang line (#!/usr/bin/env bash\r) causes the kernel to look for
    /// a "bash\r" binary, producing "/usr/bin/env: 'bash\r': No such file or directory"
    /// and killing the container immediately on startup.
    /// The <c>InjectEntrypointAsync</c> method strips \r at runtime, but this test
    /// ensures the source file itself stays clean to prevent silent build regressions.
    /// </summary>
    [Fact]
    public void EntrypointSh_HasNoCarriageReturns()
    {
        var content = ReadEntrypoint();
        Assert.False(
            content.Contains('\r'),
            "entrypoint.sh must not contain CR (\\r) characters. " +
            "CRLF line endings break the shebang on Linux, causing 'bash\\r: No such file or directory'. " +
            "Ensure the file uses LF-only line endings (add *.sh text eol=lf to .gitattributes).");
    }

    /// <summary>
    /// Verifies that entrypoint.sh does NOT fall back to a bare <c>git clone</c> (no <c>--branch</c>)
    /// when the configured base branch does not exist in the remote. A silent bare-clone fallback
    /// hides the misconfiguration (e.g. repo uses "master" but IssuePit is configured with "main")
    /// and makes the failure hard to diagnose.
    /// Instead the script must emit a clear error and exit so the container exits non-zero and
    /// EnsureContainerRunningAsync surfaces a useful message in the session logs.
    /// </summary>
    [Fact]
    public void EntrypointSh_BaseBranchNotFound_EmitsErrorAndDoesNotFallBack()
    {
        var content = ReadEntrypoint();

        // Must NOT contain a bare clone (no --branch) — that would silently hide a misconfigured DefaultBranch.
        Assert.DoesNotContain("git clone --depth=1 \"${CLONE_URL}\" \"${WORKSPACE}\"", content,
            StringComparison.Ordinal);

        // Must contain a clear error message before failing.
        Assert.Contains("Update GitRepository.DefaultBranch", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that <c>npm install</c> in entrypoint.sh is non-fatal.
    /// An OOM-killed or otherwise failing <c>npm install</c> (exit code 137) must not
    /// propagate through <c>set -euo pipefail</c> and kill the container.
    /// </summary>
    [Fact]
    public void EntrypointSh_NpmInstall_IsNonFatal()
    {
        var content = ReadEntrypoint();

        // Only match lines that actually invoke npm install (not echo lines that mention it).
        var npmLines = content.Split('\n')
            .Where(l => System.Text.RegularExpressions.Regex.IsMatch(l.TrimStart(), @"^npm\s+install"))
            .ToList();
        Assert.True(npmLines.Count > 0, "entrypoint.sh must contain a 'npm install' invocation line.");

        foreach (var line in npmLines)
            Assert.True(line.Contains("||"),
                $"npm install in entrypoint.sh must be non-fatal (use '||' to handle failures). " +
                $"A failed npm install (e.g. OOM-killed with exit 137) must not kill the container " +
                $"via set -euo pipefail. Offending line: '{line.Trim()}'");
    }

    /// <summary>
    /// Verifies that <c>dotnet restore</c> in entrypoint.sh is non-fatal.
    /// An OOM-killed or otherwise failing <c>dotnet restore</c> must not kill the container.
    /// </summary>
    [Fact]
    public void EntrypointSh_DotnetRestore_IsNonFatal()
    {
        var content = ReadEntrypoint();

        // Only match lines that actually invoke dotnet restore (not echo/comment lines).
        var restoreLines = content.Split('\n')
            .Where(l => System.Text.RegularExpressions.Regex.IsMatch(l.TrimStart(), @"^dotnet\s+restore"))
            .ToList();
        Assert.True(restoreLines.Count > 0, "entrypoint.sh must contain a 'dotnet restore' invocation line.");

        foreach (var line in restoreLines)
            Assert.True(line.Contains("||"),
                $"dotnet restore in entrypoint.sh must be non-fatal (use '||' to handle failures). " +
                $"A failed dotnet restore (e.g. OOM-killed with exit 137) must not kill the container " +
                $"via set -euo pipefail. Offending line: '{line.Trim()}'");
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
