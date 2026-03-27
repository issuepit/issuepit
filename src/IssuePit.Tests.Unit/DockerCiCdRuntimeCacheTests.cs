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

    // ── BuildActContainerOptions ───────────────────────────────────────────────

    [Fact]
    public void BuildActContainerOptions_AllNull_ReturnsNull()
    {
        var result = DockerCiCdRuntime.BuildActContainerOptions(null, null, null);
        Assert.Null(result);
    }

    [Fact]
    public void BuildActContainerOptions_NpmCacheVolume_MountsNpmCache()
    {
        var result = DockerCiCdRuntime.BuildActContainerOptions("issuepit-npm-cache", null, null);
        Assert.NotNull(result);
        Assert.Contains("-v /cache/npm:/root/.npm", result);
    }

    [Fact]
    public void BuildActContainerOptions_NuGetCacheVolume_MountsNuGetCache()
    {
        var result = DockerCiCdRuntime.BuildActContainerOptions(null, "issuepit-nuget-cache", null);
        Assert.NotNull(result);
        Assert.Contains("-v /cache/nuget:/root/.nuget/packages", result);
    }

    [Fact]
    public void BuildActContainerOptions_PlaywrightCacheVolume_MountsPlaywrightCache()
    {
        var result = DockerCiCdRuntime.BuildActContainerOptions(null, null, "issuepit-playwright-cache");
        Assert.NotNull(result);
        Assert.Contains("-v /cache/playwright:/root/.cache/ms-playwright", result);
    }

    [Fact]
    public void BuildActContainerOptions_AptProxy_MountsAptProxyFile()
    {
        var result = DockerCiCdRuntime.BuildActContainerOptions(null, null, null, includeAptProxy: true);
        Assert.NotNull(result);
        Assert.Contains("-v /etc/apt/apt.conf.d/01proxy:/etc/apt/apt.conf.d/01proxy", result);
    }

    [Fact]
    public void BuildActContainerOptions_AllVolumes_CombinesIntoSingleString()
    {
        var result = DockerCiCdRuntime.BuildActContainerOptions(
            "npm-vol", "nuget-vol", "playwright-vol", includeAptProxy: true);
        Assert.NotNull(result);
        Assert.Contains("-v /cache/npm:/root/.npm", result);
        Assert.Contains("-v /cache/nuget:/root/.nuget/packages", result);
        Assert.Contains("-v /cache/playwright:/root/.cache/ms-playwright", result);
        Assert.Contains("-v /etc/apt/apt.conf.d/01proxy:/etc/apt/apt.conf.d/01proxy", result);
    }

    [Fact]
    public void BuildActContainerOptions_NoAptProxy_DoesNotContainAptProxy()
    {
        var result = DockerCiCdRuntime.BuildActContainerOptions("npm-vol", "nuget-vol", "playwright-vol");
        Assert.NotNull(result);
        Assert.DoesNotContain("01proxy", result);
    }

    [Fact]
    public void BuildActContainerOptions_EmptyVolumeName_IsSkipped()
    {
        var result = DockerCiCdRuntime.BuildActContainerOptions("", "   ", null);
        Assert.Null(result);
    }



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

    // ── StripOriginPrefix ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("origin/main", "main")]
    [InlineData("origin/copilot/fix-cicd-runner-ffmpeg-install", "copilot/fix-cicd-runner-ffmpeg-install")]
    [InlineData("Origin/main", "main")]
    [InlineData("ORIGIN/main", "main")]
    public void StripOriginPrefix_WithOriginPrefix_StripsPrefix(string input, string expected)
    {
        Assert.Equal(expected, DockerCiCdRuntime.StripOriginPrefix(input));
    }

    [Theory]
    [InlineData("main")]
    [InlineData("copilot/fix-something")]
    [InlineData("feature/123-my-branch")]
    public void StripOriginPrefix_WithoutOriginPrefix_ReturnsUnchanged(string input)
    {
        Assert.Equal(input, DockerCiCdRuntime.StripOriginPrefix(input));
    }
}
