using IssuePit.Core.Enums;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class ApiKeyProviderEnumTests
{
    [Fact]
    public void ApiKeyProvider_OpenRouter_HasExpectedValue()
    {
        Assert.Equal(8, (int)ApiKeyProvider.OpenRouter);
    }

    [Fact]
    public void ApiKeyProvider_AllExpectedProvidersAreDefined()
    {
        var providers = Enum.GetValues<ApiKeyProvider>();
        Assert.Contains(ApiKeyProvider.OpenRouter, providers);
        Assert.Contains(ApiKeyProvider.OpenAi, providers);
        Assert.Contains(ApiKeyProvider.Anthropic, providers);
    }
}
