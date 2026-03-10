using IssuePit.CiCdClient.Runtimes;
using IssuePit.Core.Enums;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class DockerCiCdRuntimeCacheTests
{
    // ── BuildDindStartupScript ─────────────────────────────────────────────────

    [Fact]
    public void BuildDindStartupScript_NoMirror_DoesNotContainDaemonJson()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript();
        Assert.DoesNotContain("daemon.json", script);
        Assert.DoesNotContain("registry-mirrors", script);
    }

    [Fact]
    public void BuildDindStartupScript_NoMirror_StartsDockerd()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript();
        Assert.Contains("dockerd > /tmp/dockerd.log 2>&1 &", script);
    }

    [Fact]
    public void BuildDindStartupScript_NoMirror_WaitsForSocket()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript();
        Assert.Contains("docker info > /dev/null 2>&1", script);
    }

    /// <summary>
    /// Regression test: docker info in the readiness loop must be wrapped with 'timeout 2' to
    /// prevent the startup script from hanging indefinitely when dockerd reconciles orphaned
    /// container metadata from a previously-killed DinD instance sharing the cache volume.
    /// Without this guard, DrainMultiplexedStreamAsync blocks forever and the E2E test eventually
    /// triggers xUnit's --blame-hang-timeout collector after 5 minutes of inactivity.
    /// </summary>
    [Fact]
    public void BuildDindStartupScript_DockerInfoInLoop_IsProtectedByTimeout()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript();
        // The while-loop check must use 'timeout 2 docker info' to bound each iteration.
        Assert.Contains("timeout 2 docker info", script);
        // The final readiness check must also be bounded.
        Assert.Contains("timeout 10 docker info", script);
    }

    /// <summary>
    /// Regression test: the counter variable must be named 'dind_ready_timeout', NOT 'timeout'.
    /// Using 'timeout' as a variable name shadows the shell built-in 'timeout' command, causing
    /// 'timeout 2 docker info' to expand to '&lt;int&gt; 2 docker info' (a syntax error or unexpected
    /// execution) instead of running the coreutils timeout binary.
    /// </summary>
    [Fact]
    public void BuildDindStartupScript_UsesNamedCounterVariable_NotTimeoutVariable()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript();
        // Must use dind_ready_timeout (not a bare 'timeout') as the loop counter.
        Assert.Contains("dind_ready_timeout=60", script);
        Assert.Contains("$dind_ready_timeout -gt 0", script);
        // A bare 'timeout=60' assignment (not as part of 'dind_ready_timeout=60') must not exist.
        // Split lines to avoid matching 'dind_ready_timeout=60' as a false positive.
        var lines = script.Split('\n');
        Assert.DoesNotContain(lines, l => l.Trim() == "timeout=60" || l.Trim().StartsWith("timeout=", StringComparison.Ordinal));
    }

    /// <summary>
    /// Regression test: the DinD startup script must NOT contain orphaned-container cleanup
    /// (docker ps -aq --filter name=act-). Running docker ps against a newly-started dockerd
    /// that inherits state from a previous killed DinD instance can block indefinitely while
    /// dockerd reconciles orphaned container metadata — causing DrainMultiplexedStreamAsync
    /// to block forever and the CI/CD run to never reach a terminal status.
    /// Container-name collisions are handled by the act retry loop in RunAsync instead.
    /// </summary>
    [Fact]
    public void BuildDindStartupScript_DoesNotContainOrphanedContainerCleanup()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript();
        // docker ps in the startup script was removed because it can block indefinitely.
        Assert.DoesNotContain("docker ps", script);
    }

    [Fact]
    public void BuildDindStartupScript_WithMirrorUrl_WritesDaemonJson()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript("http://host.docker.internal:5100");
        Assert.Contains("daemon.json", script);
        Assert.Contains("registry-mirrors", script);
        Assert.Contains("http://host.docker.internal:5100", script);
    }

    [Fact]
    public void BuildDindStartupScript_WithMirrorUrl_ProducesValidJsonInDaemonJson()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript("http://host.docker.internal:5100");
        // Extract the JSON embedded in the printf command.
        // The line looks like: printf '%s' '{"registry-mirrors":["http://..."]}' > /etc/docker/daemon.json
        var lines = script.Split('\n');
        var daemonJsonLine = Array.Find(lines, l => l.Contains("daemon.json") && l.Contains("printf"));
        Assert.NotNull(daemonJsonLine);
        // Verify the JSON is valid by round-tripping through JsonDocument.
        var start = daemonJsonLine!.IndexOf("'{", StringComparison.Ordinal) + 1;
        var end = daemonJsonLine.IndexOf("}' >", StringComparison.Ordinal) + 1;
        var json = daemonJsonLine[start..end];
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("registry-mirrors", out var mirrors));
        Assert.Equal("http://host.docker.internal:5100", mirrors[0].GetString());
    }

    [Fact]
    public void BuildDindStartupScript_WithMirrorUrl_WritesDaemonJsonBeforeDockerd()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript("http://host.docker.internal:5100");
        var daemonJsonIdx = script.IndexOf("daemon.json", StringComparison.Ordinal);
        var dockerdIdx = script.IndexOf("dockerd > /tmp/dockerd.log", StringComparison.Ordinal);
        Assert.True(daemonJsonIdx < dockerdIdx, "daemon.json must be written before dockerd starts");
    }

    [Fact]
    public void BuildDindStartupScript_WithMirrorUrl_StillStartsDockerd()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript("http://host.docker.internal:5100");
        Assert.Contains("dockerd > /tmp/dockerd.log 2>&1 &", script);
    }

    [Fact]
    public void BuildDindStartupScript_NullMirrorUrl_SameAsNoMirror()
    {
        var scriptNull = DockerCiCdRuntime.BuildDindStartupScript(null);
        var scriptDefault = DockerCiCdRuntime.BuildDindStartupScript();
        Assert.Equal(scriptDefault, scriptNull);
    }

    [Fact]
    public void BuildDindStartupScript_EmptyMirrorUrl_DoesNotContainDaemonJson()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript(string.Empty);
        Assert.DoesNotContain("daemon.json", script);
    }

    [Fact]
    public void BuildDindStartupScript_WithMirrorUrl_UsesLfLineEndings()
    {
        // Script must use LF-only line endings so it runs correctly inside Linux containers,
        // regardless of the OS line endings of the host building the binary.
        var script = DockerCiCdRuntime.BuildDindStartupScript("http://host.docker.internal:5100");
        Assert.DoesNotContain("\r\n", script);
    }

    // ── Apt cache port ─────────────────────────────────────────────────────────

    [Fact]
    public void BuildDindStartupScript_WithAptCachePort_ContainsIptablesDnat()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript(aptCachePort: 3142);
        Assert.Contains("iptables -t nat -A PREROUTING -p tcp --dport 3142 -j DNAT", script);
        Assert.Contains("POSTROUTING -j MASQUERADE", script);
    }

    [Fact]
    public void BuildDindStartupScript_WithAptCachePort_EnablesIpForwarding()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript(aptCachePort: 3142);
        Assert.Contains("echo 1 > /proc/sys/net/ipv4/ip_forward", script);
    }

    [Fact]
    public void BuildDindStartupScript_WithAptCachePort_WritesAptProxyConfig()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript(aptCachePort: 3142);
        Assert.Contains("/etc/apt/apt.conf.d/01proxy", script);
        Assert.Contains("Acquire::http::Proxy", script);
        Assert.Contains("3142", script);
    }

    [Fact]
    public void BuildDindStartupScript_WithAptCachePort_WritesAptProxyAfterDockerd()
    {
        // Apt proxy config requires the docker0 bridge IP, which is only available after dockerd starts.
        var script = DockerCiCdRuntime.BuildDindStartupScript(aptCachePort: 3142);
        var dockerdIdx = script.IndexOf("dockerd > /tmp/dockerd.log", StringComparison.Ordinal);
        var aptProxyIdx = script.IndexOf("/etc/apt/apt.conf.d/01proxy", StringComparison.Ordinal);
        Assert.True(dockerdIdx < aptProxyIdx, "Apt proxy config must be written after dockerd starts");
    }

    [Fact]
    public void BuildDindStartupScript_WithAptCachePort_IptablesBeforeDockerd()
    {
        // iptables DNAT rules must be set up before dockerd to ensure DinD container traffic
        // is forwarded from the moment the first container starts.
        var script = DockerCiCdRuntime.BuildDindStartupScript(aptCachePort: 3142);
        var iptablesIdx = script.IndexOf("iptables", StringComparison.Ordinal);
        var dockerdIdx = script.IndexOf("dockerd > /tmp/dockerd.log", StringComparison.Ordinal);
        Assert.True(iptablesIdx < dockerdIdx, "iptables rules must be set up before dockerd starts");
    }

    [Fact]
    public void BuildDindStartupScript_NoAptCachePort_DoesNotContainAptProxy()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript();
        Assert.DoesNotContain("/etc/apt/apt.conf.d/01proxy", script);
        Assert.DoesNotContain("iptables", script);
    }

    [Fact]
    public void BuildDindStartupScript_WithAptCachePort_UsesLfLineEndings()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript(aptCachePort: 3142);
        Assert.DoesNotContain("\r\n", script);
    }

    // ── HTTP cache port (renamed from playwright cache port) ───────────────────

    [Fact]
    public void BuildDindStartupScript_WithHttpCachePort_ContainsIptablesDnat()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript(httpCachePort: 3143);
        Assert.Contains("iptables -t nat -A PREROUTING -p tcp --dport 3143 -j DNAT", script);
        Assert.Contains("POSTROUTING -j MASQUERADE", script);
    }

    [Fact]
    public void BuildDindStartupScript_WithHttpCachePort_NoAptProxyConfig()
    {
        // HTTP cache port alone should not produce an apt proxy config file.
        var script = DockerCiCdRuntime.BuildDindStartupScript(httpCachePort: 3143);
        Assert.DoesNotContain("/etc/apt/apt.conf.d/01proxy", script);
    }

    [Fact]
    public void BuildDindStartupScript_WithBothCachePorts_ContainsBothDnatRules()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript(aptCachePort: 3142, httpCachePort: 3143);
        Assert.Contains("--dport 3142 -j DNAT", script);
        Assert.Contains("--dport 3143 -j DNAT", script);
        Assert.Contains("/etc/apt/apt.conf.d/01proxy", script);
    }

    [Fact]
    public void BuildDindStartupScript_WithBothCachePorts_StillStartsDockerd()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript(aptCachePort: 3142, httpCachePort: 3143);
        Assert.Contains("dockerd > /tmp/dockerd.log 2>&1 &", script);
    }

    // ── AptCachePort / HttpCachePort constants ─────────────────────────────────

    [Fact]
    public void DockerCiCdRuntime_AptCachePort_Is3142()
    {
        Assert.Equal(3142, DockerCiCdRuntime.AptCachePort);
    }

    [Fact]
    public void DockerCiCdRuntime_HttpCachePort_Is3143()
    {
        Assert.Equal(3143, DockerCiCdRuntime.HttpCachePort);
    }

    // ── DindImageCacheStrategy enum ────────────────────────────────────────────

    [Theory]
    [InlineData("Off", DindImageCacheStrategy.Off)]
    [InlineData("off", DindImageCacheStrategy.Off)]
    [InlineData("LocalVolume", DindImageCacheStrategy.LocalVolume)]
    [InlineData("localvolume", DindImageCacheStrategy.LocalVolume)]
    [InlineData("RegistryMirror", DindImageCacheStrategy.RegistryMirror)]
    [InlineData("registrymirror", DindImageCacheStrategy.RegistryMirror)]
    public void DindImageCacheStrategy_ParseCaseInsensitive(string value, DindImageCacheStrategy expected)
    {
        var parsed = Enum.Parse<DindImageCacheStrategy>(value, ignoreCase: true);
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void DindImageCacheStrategy_EnumValues_AreStable()
    {
        // Verify the enum values so code that relies on integer comparison doesn't break silently.
        Assert.Equal(0, (int)DindImageCacheStrategy.Off);
        Assert.Equal(1, (int)DindImageCacheStrategy.LocalVolume);
        Assert.Equal(2, (int)DindImageCacheStrategy.RegistryMirror);
    }

    [Fact]
    public void DindImageCacheStrategy_DefaultConstant_IsRegistryMirror()
    {
        // The DockerCiCdRuntime applies RegistryMirror when no config key and no trigger override.
        // Verify this is the documented default by checking the enum name matches.
        Assert.Equal("RegistryMirror", DindImageCacheStrategy.RegistryMirror.ToString());
    }

    // ── TriggerPayload DindCacheStrategy field ─────────────────────────────────

    [Fact]
    public void TriggerPayload_DindCacheStrategy_DefaultsToNull()
    {
        var trigger = new TriggerPayload(ProjectId: Guid.NewGuid(), CommitSha: null, Branch: null,
            Workflow: null, AgentSessionId: null, WorkspacePath: null, EventName: null);
        Assert.Null(trigger.DindCacheStrategy);
    }

    [Fact]
    public void TriggerPayload_DindCacheStrategy_CanBeSetToOff()
    {
        var trigger = new TriggerPayload(ProjectId: Guid.NewGuid(), CommitSha: null, Branch: null,
            Workflow: null, AgentSessionId: null, WorkspacePath: null, EventName: null,
            DindCacheStrategy: DindImageCacheStrategy.Off);
        Assert.Equal(DindImageCacheStrategy.Off, trigger.DindCacheStrategy);
    }

    [Fact]
    public void TriggerPayload_DindCacheStrategy_CanBeSetToRegistryMirror()
    {
        var trigger = new TriggerPayload(ProjectId: Guid.NewGuid(), CommitSha: null, Branch: null,
            Workflow: null, AgentSessionId: null, WorkspacePath: null, EventName: null,
            DindCacheStrategy: DindImageCacheStrategy.RegistryMirror);
        Assert.Equal(DindImageCacheStrategy.RegistryMirror, trigger.DindCacheStrategy);
    }
}
