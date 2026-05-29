using System.Net;
using System.Text;
using System.Text.Json;
using IssuePit.McpServer;
using IssuePit.McpServer.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class McpIssueReviewQueueToolsTests
{
    [Fact]
    public async Task UpdateIssue_QueuesChange_WithoutCallingPutImmediately()
    {
        var handler = new RecordingHandler(req =>
        {
            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath.StartsWith("/api/issues/") == true)
                return Json(new { id = Guid.NewGuid(), title = "Current title", body = "Current body" });
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var (tools, queue) = CreateTools(handler);

        var response = await tools.UpdateIssue(Guid.NewGuid(), "New title", "New body");
        var payload = JsonDocument.Parse(response).RootElement;

        Assert.True(payload.GetProperty("queued").GetBoolean());
        Assert.True(payload.TryGetProperty("reviewId", out _));
        Assert.Single(queue.List());
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task ApproveIssueChange_AllowsPartialFieldApproval()
    {
        var issueId = Guid.NewGuid();
        var handler = new RecordingHandler(req =>
        {
            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/api/issues/{issueId}")
                return Json(new { id = issueId, title = "Current title", body = "Current body" });

            if (req.Method == HttpMethod.Put && req.RequestUri?.AbsolutePath == $"/api/issues/{issueId}")
            {
                return Json(new { id = issueId, title = "Approved title" });
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var (tools, queue) = CreateTools(handler);
        await tools.UpdateIssue(issueId, "Approved title", "Rejected body");
        var queued = Assert.Single(queue.List());

        await tools.ApproveIssueChange(queued.Id, applyTitle: true, applyBody: false, applyStatus: false, applyPriority: false, applyType: false);

        var putCall = Assert.Single(handler.Calls, c => c.Method == HttpMethod.Put);
        var putBody = putCall.Body;
        Assert.NotNull(putBody);
        var body = JsonDocument.Parse(putBody!).RootElement;
        Assert.True(body.TryGetProperty("title", out _));
        Assert.False(body.TryGetProperty("body", out _));
        Assert.False(body.TryGetProperty("status", out _));
        Assert.False(body.TryGetProperty("priority", out _));
        Assert.False(body.TryGetProperty("type", out _));
        Assert.Empty(queue.List());
    }

    [Fact]
    public async Task GetIssue_ReturnsResultImmediately_WithoutQueueing()
    {
        var issueId = Guid.NewGuid();
        var handler = new RecordingHandler(req =>
        {
            if (req.Method == HttpMethod.Get && req.RequestUri?.AbsolutePath == $"/api/issues/{issueId}")
                return Json(new { id = issueId, title = "Issue title" });
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var (tools, queue) = CreateTools(handler);
        var response = await tools.GetIssue(issueId);

        Assert.Contains("Issue title", response);
        Assert.Empty(queue.List());
    }

    private static (IssueTools Tools, McpIssueChangeReviewQueue Queue) CreateTools(RecordingHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var api = new IssuePitApiClient(http);
        var options = Options.Create(new McpServerOptions());
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var requestContext = new McpRequestContext(accessor);
        var queue = new McpIssueChangeReviewQueue();
        return (new IssueTools(api, options, requestContext, queue), queue);
    }

    private static HttpResponseMessage Json<T>(T value)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<RecordedCall> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add(new RecordedCall(request.Method, request.RequestUri?.AbsolutePath ?? string.Empty, body));
            return responder(request);
        }
    }

    private sealed record RecordedCall(HttpMethod Method, string Path, string? Body);
}
