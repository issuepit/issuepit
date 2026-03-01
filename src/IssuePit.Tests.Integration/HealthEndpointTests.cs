using System.Net;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetHealthz_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAlivez_ReturnsOk()
    {
        var response = await _client.GetAsync("/alive");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
