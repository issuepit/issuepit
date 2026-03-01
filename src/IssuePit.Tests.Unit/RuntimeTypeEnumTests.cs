using IssuePit.Core.Enums;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class RuntimeTypeEnumTests
{
    [Fact]
    public void RuntimeType_OpenCodeCli_HasExpectedValue()
    {
        Assert.Equal(6, (int)RuntimeType.OpenCodeCli);
    }

    [Fact]
    public void RuntimeType_AllExpectedTypesAreDefined()
    {
        var types = Enum.GetValues<RuntimeType>();
        Assert.Contains(RuntimeType.OpenCodeCli, types);
        Assert.Contains(RuntimeType.Native, types);
        Assert.Contains(RuntimeType.Docker, types);
        Assert.Contains(RuntimeType.OpenSandbox, types);
    }
}
