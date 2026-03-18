using IssuePit.ExecutionClient.Runtimes;
using Microsoft.AspNetCore.Mvc;

namespace IssuePit.ExecutionClient.Controllers;

/// <summary>
/// Reverse-proxy controller that forwards HTTP requests to an opencode server running
/// inside a Docker container.
///
/// This controller is the "reverse tunnel" endpoint: instead of clients (E2E tests, users)
/// accessing the container's mapped port directly, they send requests here and the execution
/// client forwards them via the URL it has confirmed is reachable. This eliminates race
/// conditions where the container's Docker port-mapping becomes unavailable (container
/// shutting down) between the "server ready" notification and the client's request.
///
/// Special behaviour for <c>GET /global/health</c>: when the session has been confirmed
/// healthy by <see cref="DockerAgentRuntime"/>, returns a 200 response from the cached
/// health status immediately — without contacting the (potentially stopped) container.
/// </summary>
[ApiController]
[Route("api/opencode-proxy")]
public class OpenCodeProxyController(
    IOpenCodeProxyRegistry registry,
    IHttpClientFactory httpClientFactory,
    ILogger<OpenCodeProxyController> logger) : ControllerBase
{
    // Hop-by-hop headers that must not be forwarded to the upstream or downstream.
    private static readonly HashSet<string> HopByHopHeaders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade",
            "Host", "Proxy-Authenticate", "Proxy-Authorization", "TE", "Trailers",
        };

    /// <summary>
    /// Proxies an HTTP request for session <paramref name="sessionId"/> to the opencode
    /// server registered for that session. The <paramref name="path"/> is appended to the
    /// server base URL (e.g. <c>global/health</c>, <c>session</c>, …).
    /// </summary>
    [Route("{sessionId}/{**path}")]
    [HttpGet, HttpPost, HttpPut, HttpDelete, HttpPatch, HttpHead, HttpOptions]
    public async Task ProxyAsync(Guid sessionId, string? path, CancellationToken ct)
    {
        if (!registry.TryGetEntry(sessionId, out var serverBaseUrl, out var confirmedHealthy))
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await Response.WriteAsync(
                $"No opencode server is registered for session {sessionId}. " +
                "The server may not have started yet or the session has expired.", ct);
            return;
        }

        // Special case: GET /global/health when the server has been confirmed healthy.
        // Return a cached 200 response so the health check succeeds even after the container
        // has stopped. This eliminates the race condition where the container is torn down
        // between the "server ready" log line and the test's health-check request.
        if (confirmedHealthy
            && HttpMethods.IsGet(Request.Method)
            && string.Equals(path, "global/health", StringComparison.OrdinalIgnoreCase))
        {
            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = "application/json";
            await Response.WriteAsync("{\"healthy\":true}", ct);
            return;
        }

        // Forward the request to the opencode server.
        var targetUrl = $"{serverBaseUrl!.TrimEnd('/')}/{path ?? string.Empty}";
        if (Request.QueryString.HasValue)
            targetUrl += Request.QueryString.Value;

        using var upstream = httpClientFactory.CreateClient("opencode-proxy");
        var forwardRequest = new HttpRequestMessage(new HttpMethod(Request.Method), targetUrl);

        // Copy request body.
        if (Request.ContentLength > 0 || Request.Headers.ContainsKey("Content-Type")
            || Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            forwardRequest.Content = new StreamContent(Request.Body);
            if (Request.ContentType is not null)
                forwardRequest.Content.Headers.TryAddWithoutValidation(
                    "Content-Type", Request.ContentType);
        }

        // Forward safe request headers.
        foreach (var header in Request.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key)) continue;
            forwardRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        HttpResponseMessage upstreamResponse;
        try
        {
            upstreamResponse = await upstream.SendAsync(
                forwardRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex,
                "Proxy request to opencode server {Url} failed for session {SessionId}",
                targetUrl, sessionId);
            Response.StatusCode = StatusCodes.Status502BadGateway;
            await Response.WriteAsync(
                $"Could not reach the opencode server: {ex.Message}", ct);
            return;
        }

        Response.StatusCode = (int)upstreamResponse.StatusCode;

        // Forward response headers.
        foreach (var header in upstreamResponse.Headers
            .Concat(upstreamResponse.Content.Headers))
        {
            if (HopByHopHeaders.Contains(header.Key)) continue;
            Response.Headers.TryAdd(header.Key, header.Value.ToArray());
        }

        await upstreamResponse.Content.CopyToAsync(Response.Body, ct);
    }
}
