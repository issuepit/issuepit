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
public class OpenCodeHttpApiTests
{
    private const string BaseUrl = "http://localhost:4096";

    private static OpenCodeHttpApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<OpenCodeHttpApi>.Instance);

    // ──────────────────────────────────────────────────────────────────────────
    // IsReadyAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IsReadyAsync_ServerReturns200_ReturnsTrue()
    {
        var api = CreateApi(new FakeHandler(HttpStatusCode.OK, "[]"));
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

    // ──────────────────────────────────────────────────────────────────────────
    // GetServerInfoAsync
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
    // CreateSessionAsync
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

    // ──────────────────────────────────────────────────────────────────────────
    // ListSessionsAsync
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
}
