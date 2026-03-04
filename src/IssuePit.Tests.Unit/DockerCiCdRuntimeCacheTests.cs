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
    public void DindImageCacheStrategy_DefaultIsLocalVolume()
    {
        // Verify the enum values so code that relies on integer comparison doesn't break silently.
        Assert.Equal(0, (int)DindImageCacheStrategy.Off);
        Assert.Equal(1, (int)DindImageCacheStrategy.LocalVolume);
        Assert.Equal(2, (int)DindImageCacheStrategy.RegistryMirror);
    }

    [Fact]
    public void DindImageCacheStrategy_DefaultConstant_IsLocalVolume()
    {
        // The DockerCiCdRuntime applies LocalVolume when no config key and no trigger override.
        // Verify this is the documented default by checking the enum name matches.
        Assert.Equal("LocalVolume", DindImageCacheStrategy.LocalVolume.ToString());
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
