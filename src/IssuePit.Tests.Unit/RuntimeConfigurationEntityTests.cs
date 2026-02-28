using IssuePit.Core.Entities;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class RuntimeConfigurationEntityTests
{
    [Fact]
    public void RuntimeConfiguration_IsDefault_FalseByDefault()
    {
        var runtimeConfig = new RuntimeConfiguration();
        Assert.False(runtimeConfig.IsDefault);
    }

    [Fact]
    public void RuntimeConfiguration_Configuration_DefaultsToEmptyJson()
    {
        var runtimeConfig = new RuntimeConfiguration();
        Assert.Equal("{}", runtimeConfig.Configuration);
    }
}
