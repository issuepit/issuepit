using System.Net;
using System.Text;
using IssuePit.ExecutionClient.Runtimes;
using Microsoft.Extensions.Logging.Abstractions;

namespace IssuePit.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="OpenCodeHttpApi"/>.
/// Uses a fake <see cref="HttpMessageHandler"/> to simulate opencode HTTP server responses
/// without requiring a real running server.
/// </summary>
[Trait("Category", "Unit")]
public class OpenCodeHttpApiTests
{
    private const string BaseUrl = "http://localhost:4096";

    private static OpenCodeHttpApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<OpenCodeHttpApi>.Instance);

    // ──────────────────────────────────────────────────────────────────────────
    // IsReadyAsync — uses GET /global/health
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IsReadyAsync_ServerReturns200_ReturnsTrue()
    {
        var api = CreateApi(new FakeHandler(HttpStatusCode.OK, """{"healthy":true,"version":"1.0.0"}"""));
        var result = await api.IsReadyAsync(BaseUrl, CancellationToken.None);
        Assert.True(result);
    }

    [Fact]
    public async Task IsReadyAsync_ServerReturns500_ReturnsFalse()
    {
        var api = CreateApi(new FakeHandler(HttpStatusCode.InternalServerError, ""));
        var result = await api.IsReadyAsync(BaseUrl, CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task IsReadyAsync_ConnectionRefused_ReturnsFalse()
    {
        var api = CreateApi(new ThrowingHandler(new HttpRequestException("Connection refused")));
        var result = await api.IsReadyAsync(BaseUrl, CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task IsReadyAsync_UsesGlobalHealthEndpoint()
    {
        string? requestedUrl = null;
        var api = CreateApi(new CapturingHandler(HttpStatusCode.OK, """{"healthy":true,"version":"0.1"}""",
            url => requestedUrl = url));
        await api.IsReadyAsync(BaseUrl, CancellationToken.None);
        Assert.NotNull(requestedUrl);
        Assert.Contains("/global/health", requestedUrl);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GetServerInfoAsync — uses GET /session
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetServerInfoAsync_Returns200_ReturnsBody()
    {
        const string body = """[{"id":"ses_abc","title":"test"}]""";
        var api = CreateApi(new FakeHandler(HttpStatusCode.OK, body));
        var result = await api.GetServerInfoAsync(BaseUrl, CancellationToken.None);
        Assert.Equal(body, result);
    }

    [Fact]
    public async Task GetServerInfoAsync_Returns500_ReturnsNull()
    {
        var api = CreateApi(new FakeHandler(HttpStatusCode.InternalServerError, ""));
        var result = await api.GetServerInfoAsync(BaseUrl, CancellationToken.None);
        Assert.Null(result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CreateSessionAsync — uses POST /session (no /v1/ prefix)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSessionAsync_Returns200WithId_ReturnsSessionId()
    {
        const string body = """{"id":"ses_12345","title":"New session"}""";
        var api = CreateApi(new FakeHandler(HttpStatusCode.OK, body));
        var sessionId = await api.CreateSessionAsync(BaseUrl, CancellationToken.None);
        Assert.Equal("ses_12345", sessionId);
    }

    [Fact]
    public async Task CreateSessionAsync_Returns201WithId_ReturnsSessionId()
    {
        const string body = """{"id":"ses_created","title":"Created"}""";
        var api = CreateApi(new FakeHandler(HttpStatusCode.Created, body));
        var sessionId = await api.CreateSessionAsync(BaseUrl, CancellationToken.None);
        Assert.Equal("ses_created", sessionId);
    }

    [Fact]
    public async Task CreateSessionAsync_UsesSessionEndpointWithoutV1Prefix()
    {
        string? requestedUrl = null;
        var api = CreateApi(new CapturingHandler(HttpStatusCode.OK, """{"id":"ses_x"}""",
            url => requestedUrl = url));
        await api.CreateSessionAsync(BaseUrl, CancellationToken.None);
        Assert.NotNull(requestedUrl);
        Assert.DoesNotContain("/v1/", requestedUrl);
        Assert.Contains("/session", requestedUrl);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SendMessageAsync — uses POST /session/{id}/prompt_async
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_Returns204_Succeeds()
    {
        var api = CreateApi(new FakeHandler(HttpStatusCode.NoContent, ""));
        // Should not throw
        await api.SendMessageAsync(BaseUrl, "ses_abc", "Fix the bug", CancellationToken.None);
    }

    [Fact]
    public async Task SendMessageAsync_UsesPromptAsyncEndpoint()
    {
        string? requestedUrl = null;
        var api = CreateApi(new CapturingHandler(HttpStatusCode.NoContent, "",
            url => requestedUrl = url));
        await api.SendMessageAsync(BaseUrl, "ses_abc", "Fix the bug", CancellationToken.None);
        Assert.NotNull(requestedUrl);
        Assert.Contains("ses_abc/prompt_async", requestedUrl);
        Assert.DoesNotContain("/v1/", requestedUrl);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ListSessionsAsync — uses GET /session (no /v1/ prefix)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListSessionsAsync_EmptyArray_ReturnsEmptyList()
    {
        var api = CreateApi(new FakeHandler(HttpStatusCode.OK, "[]"));
        var sessions = await api.ListSessionsAsync(BaseUrl, CancellationToken.None);
        Assert.Empty(sessions);
    }

    [Fact]
    public async Task ListSessionsAsync_TwoSessions_ReturnsBothIds()
    {
        const string body = """[{"id":"ses_aaa"},{"id":"ses_bbb"}]""";
        var api = CreateApi(new FakeHandler(HttpStatusCode.OK, body));
        var sessions = await api.ListSessionsAsync(BaseUrl, CancellationToken.None);
        Assert.Equal(2, sessions.Count);
        Assert.Contains("ses_aaa", sessions);
        Assert.Contains("ses_bbb", sessions);
    }

    [Fact]
    public async Task ListSessionsAsync_SessionsMissingIdField_AreSkipped()
    {
        const string body = """[{"id":"ses_ok"},{"title":"no id here"}]""";
        var api = CreateApi(new FakeHandler(HttpStatusCode.OK, body));
        var sessions = await api.ListSessionsAsync(BaseUrl, CancellationToken.None);
        Assert.Single(sessions);
        Assert.Equal("ses_ok", sessions[0]);
    }

    [Fact]
    public async Task ListSessionsAsync_UsesSessionEndpointWithoutV1Prefix()
    {
        string? requestedUrl = null;
        var api = CreateApi(new CapturingHandler(HttpStatusCode.OK, "[]",
            url => requestedUrl = url));
        await api.ListSessionsAsync(BaseUrl, CancellationToken.None);
        Assert.NotNull(requestedUrl);
        Assert.DoesNotContain("/v1/", requestedUrl);
        Assert.Contains("/session", requestedUrl);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Fake handlers
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class FakeHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw exception;
    }

    private sealed class CapturingHandler(
        HttpStatusCode statusCode,
        string responseBody,
        Action<string> onRequest) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            onRequest(request.RequestUri?.ToString() ?? "");
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            });
        }
    }
}
