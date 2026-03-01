using System.Net;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E smoke tests that verify the API is reachable when the full Aspire stack is running.
/// </summary>
[Trait("Category", "E2E")]
[Collection("E2E")]
public class ApiSmokeTests(AspireFixture fixture)
{
    [Fact]
    public async Task Api_HealthEndpoint_ReturnsOk()
    {
        // The API maps health checks to "/health" (not "/healthz") via MapDefaultEndpoints.
        var response = await fixture.ApiClient!.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Api_AliveEndpoint_ReturnsOk()
    {
        var response = await fixture.ApiClient!.GetAsync("/alive");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Api_OpenApiEndpoint_ReturnsOk()
    {
        var response = await fixture.ApiClient!.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
