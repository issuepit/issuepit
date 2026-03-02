using System.Net;
using System.Text.Json;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class VersionEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetVersion_ReturnsOkWithVersionInfo()
    {
        var response = await _client.GetAsync("/api/version");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("version", out var versionProp));
        Assert.False(string.IsNullOrWhiteSpace(versionProp.GetString()));
    }
}
