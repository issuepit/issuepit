using IssuePit.CiCdClient.Runtimes;
using IssuePit.Core.Enums;
using Microsoft.Extensions.Configuration;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class DindCacheStrategyTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // BuildDindStartupScript
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildDindStartupScript_NoMirror_DoesNotIncludeRegistryMirrorFlag()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript();
        Assert.DoesNotContain("--registry-mirror", script);
    }

    [Fact]
    public void BuildDindStartupScript_WithMirror_IncludesRegistryMirrorFlag()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript("http://172.17.0.1:5555");
        Assert.Contains("--registry-mirror=http://172.17.0.1:5555", script);
    }

    [Fact]
    public void BuildDindStartupScript_WithNullMirror_DoesNotIncludeRegistryMirrorFlag()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript(null);
        Assert.DoesNotContain("--registry-mirror", script);
    }

    [Fact]
    public void BuildDindStartupScript_WithEmptyMirror_DoesNotIncludeRegistryMirrorFlag()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript(string.Empty);
        Assert.DoesNotContain("--registry-mirror", script);
    }

    [Fact]
    public void BuildDindStartupScript_AlwaysInstallsDockerIfMissing()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript();
        Assert.Contains("command -v dockerd", script);
    }

    [Fact]
    public void BuildDindStartupScript_AlwaysPollerSocket()
    {
        var script = DockerCiCdRuntime.BuildDindStartupScript();
        Assert.Contains("docker info", script);
        Assert.Contains("dockerd ready", script);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ResolveDindCacheStrategy
    // ──────────────────────────────────────────────────────────────────────────

    private static DockerCiCdRuntime CreateRuntime(string? strategyValue = null)
    {
        var configData = new Dictionary<string, string?>();
        if (strategyValue is not null)
            configData["CiCd__DindCache__Strategy"] = strategyValue;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // DockerCiCdRuntime requires ILogger and DockerClient — we only need to test
        // ResolveDindCacheStrategy which doesn't use them, so we pass nulls cast to the
        // required types (the method is synchronous and never calls those dependencies).
        return new DockerCiCdRuntime(
            logger: null!,
            dockerClient: null!,
            configuration: config);
    }

    [Fact]
    public void ResolveDindCacheStrategy_TriggerOverride_ReturnsOverrideValue()
    {
        var runtime = CreateRuntime("None");
        var result = runtime.ResolveDindCacheStrategy(DindCacheStrategy.RegistryMirror);
        Assert.Equal(DindCacheStrategy.RegistryMirror, result);
    }

    [Fact]
    public void ResolveDindCacheStrategy_NoTriggerNoConfig_DefaultsToVolume()
    {
        var runtime = CreateRuntime();
        var result = runtime.ResolveDindCacheStrategy(null);
        Assert.Equal(DindCacheStrategy.Volume, result);
    }

    [Fact]
    public void ResolveDindCacheStrategy_ConfigNone_ReturnsNone()
    {
        var runtime = CreateRuntime("None");
        var result = runtime.ResolveDindCacheStrategy(null);
        Assert.Equal(DindCacheStrategy.None, result);
    }

    [Fact]
    public void ResolveDindCacheStrategy_ConfigVolume_ReturnsVolume()
    {
        var runtime = CreateRuntime("Volume");
        var result = runtime.ResolveDindCacheStrategy(null);
        Assert.Equal(DindCacheStrategy.Volume, result);
    }

    [Fact]
    public void ResolveDindCacheStrategy_ConfigRegistryMirror_ReturnsRegistryMirror()
    {
        var runtime = CreateRuntime("RegistryMirror");
        var result = runtime.ResolveDindCacheStrategy(null);
        Assert.Equal(DindCacheStrategy.RegistryMirror, result);
    }

    [Fact]
    public void ResolveDindCacheStrategy_ConfigCaseInsensitive_Parsed()
    {
        var runtime = CreateRuntime("registrymirror");
        var result = runtime.ResolveDindCacheStrategy(null);
        Assert.Equal(DindCacheStrategy.RegistryMirror, result);
    }

    [Fact]
    public void ResolveDindCacheStrategy_InvalidConfig_DefaultsToVolume()
    {
        var runtime = CreateRuntime("invalid-value");
        var result = runtime.ResolveDindCacheStrategy(null);
        Assert.Equal(DindCacheStrategy.Volume, result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DindCacheStrategy enum
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DindCacheStrategy_HasExpectedValues()
    {
        var values = Enum.GetValues<DindCacheStrategy>();
        Assert.Contains(DindCacheStrategy.None, values);
        Assert.Contains(DindCacheStrategy.Volume, values);
        Assert.Contains(DindCacheStrategy.RegistryMirror, values);
    }
}
