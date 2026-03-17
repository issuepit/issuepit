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
    // WaitForCompletionAsync — polls /session/status and /session/{id}/message
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Simulates the happy path: session goes busy → idle with an assistant message.
    /// WaitForCompletionAsync must return Completed.
    /// </summary>
    [Fact]
    public async Task WaitForCompletionAsync_BusyThenIdle_ReturnsCompleted()
    {
        OpenCodeHttpApi.PollInterval = TimeSpan.FromMilliseconds(10);
        // Request sequence:
        //   1. GET /session/status → busy (seenBusy = true)
        //   2. GET /session/{id}/message → assistant message (new, emitted to log)
        //   3. GET /session/status → idle (seenBusy = true → check for completion)
        //   4. GET /session/{id}/message → same assistant message (final error check)
        const string sessionId = "ses_test";
        var statusCallCount = 0;
        var handler = new LambdaHandler(req =>
        {
            var url = req.RequestUri?.AbsolutePath ?? "";

            if (url.Contains("/session/status"))
            {
                statusCallCount++;
                return statusCallCount <= 1
                    ? JsonResponse("""{"ses_test":{"type":"busy"}}""")
                    : JsonResponse("""{"ses_test":{"type":"idle"}}""");
            }

            if (url.Contains($"/{sessionId}/message"))
            {
                return JsonResponse("""[{"info":{"role":"assistant"},"parts":[{"type":"text","text":"Done"}]}]""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var api = CreateApi(handler);
        var logs = new List<string>();
        var result = await api.WaitForCompletionAsync(BaseUrl, sessionId, l => { logs.Add(l); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal(AgentHttpSessionStatus.Completed, result);
    }

    /// <summary>
    /// Simulates the error path: session returns idle with an error on the last assistant message.
    /// WaitForCompletionAsync must return Error.
    /// </summary>
    [Fact]
    public async Task WaitForCompletionAsync_IdleWithAssistantError_ReturnsError()
    {
        OpenCodeHttpApi.PollInterval = TimeSpan.FromMilliseconds(10);
        const string sessionId = "ses_err";
        var statusCallCount = 0;
        var handler = new LambdaHandler(req =>
        {
            var url = req.RequestUri?.AbsolutePath ?? "";

            if (url.Contains("/session/status"))
            {
                statusCallCount++;
                return statusCallCount <= 1
                    ? JsonResponse("""{"ses_err":{"type":"busy"}}""")
                    : JsonResponse("""{"ses_err":{"type":"idle"}}""");
            }

            if (url.Contains($"/{sessionId}/message"))
            {
                // Assistant message has an error property (non-null).
                return JsonResponse("""[{"info":{"role":"assistant","error":{"name":"ProviderAuthError","data":{"providerID":"anthropic","message":"Invalid API key"}}},"parts":[]}]""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var api = CreateApi(handler);
        var result = await api.WaitForCompletionAsync(BaseUrl, sessionId, _ => Task.CompletedTask, CancellationToken.None);

        Assert.Equal(AgentHttpSessionStatus.Error, result);
    }

    /// <summary>
    /// Verifies that the initial idle state (before the agent picks up the task) does NOT
    /// prematurely terminate the wait. The seenBusy guard must hold until we observe "busy".
    /// </summary>
    [Fact]
    public async Task WaitForCompletionAsync_InitialIdleThenBusyThenIdle_ReturnsCompleted()
    {
        OpenCodeHttpApi.PollInterval = TimeSpan.FromMilliseconds(10);
        const string sessionId = "ses_race";
        var statusCallCount = 0;
        var handler = new LambdaHandler(req =>
        {
            var url = req.RequestUri?.AbsolutePath ?? "";

            if (url.Contains("/session/status"))
            {
                statusCallCount++;
                // First poll: idle (before task picked up); second: busy; third+: idle (done)
                return statusCallCount switch
                {
                    1 => JsonResponse("""{"ses_race":{"type":"idle"}}"""),
                    2 => JsonResponse("""{"ses_race":{"type":"busy"}}"""),
                    _ => JsonResponse("""{"ses_race":{"type":"idle"}}"""),
                };
            }

            if (url.Contains($"/{sessionId}/message"))
            {
                return JsonResponse("""[{"info":{"role":"assistant"},"parts":[{"type":"text","text":"Done"}]}]""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var api = CreateApi(handler);
        var result = await api.WaitForCompletionAsync(BaseUrl, sessionId, _ => Task.CompletedTask, CancellationToken.None);

        Assert.Equal(AgentHttpSessionStatus.Completed, result);
    }

    /// <summary>
    /// Verifies that cancellation causes WaitForCompletionAsync to return TimedOut.
    /// </summary>
    [Fact]
    public async Task WaitForCompletionAsync_Cancelled_ReturnsTimedOut()
    {
        OpenCodeHttpApi.PollInterval = TimeSpan.FromMilliseconds(10);
        using var cts = new CancellationTokenSource();
        var statusCallCount = 0;
        var handler = new LambdaHandler(req =>
        {
            statusCallCount++;
            if (statusCallCount >= 2)
                cts.Cancel();
            return JsonResponse("""{"ses_x":{"type":"busy"}}""");
        });

        var api = CreateApi(handler);
        var result = await api.WaitForCompletionAsync(BaseUrl, "ses_x", _ => Task.CompletedTask, cts.Token);

        Assert.Equal(AgentHttpSessionStatus.TimedOut, result);
    }

    /// <summary>
    /// Verifies that text parts from messages are emitted to the log callback.
    /// </summary>
    [Fact]
    public async Task WaitForCompletionAsync_AssistantTextParts_AreEmittedToLog()
    {
        OpenCodeHttpApi.PollInterval = TimeSpan.FromMilliseconds(10);
        const string sessionId = "ses_log";
        var statusCallCount = 0;
        var handler = new LambdaHandler(req =>
        {
            var url = req.RequestUri?.AbsolutePath ?? "";

            if (url.Contains("/session/status"))
            {
                statusCallCount++;
                return statusCallCount <= 1
                    ? JsonResponse("""{"ses_log":{"type":"busy"}}""")
                    : JsonResponse("""{"ses_log":{"type":"idle"}}""");
            }

            if (url.Contains($"/{sessionId}/message"))
            {
                return JsonResponse("""[{"info":{"role":"assistant"},"parts":[{"type":"text","text":"Hello world"}]}]""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var api = CreateApi(handler);
        var logs = new List<string>();
        await api.WaitForCompletionAsync(BaseUrl, sessionId, l => { logs.Add(l); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Contains(logs, l => l.Contains("Hello world"));
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

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

    private sealed class LambdaHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
