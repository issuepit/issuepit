namespace IssuePit.McpServer;

/// <summary>
/// Delegating handler that reads an MCP bearer token from the current HTTP request context
/// and forwards it as the <c>X-Mcp-Token</c> header on all outgoing IssuePit API calls.
/// This allows the API to validate the token and resolve the tenant / read-only flag.
/// </summary>
public class McpTokenForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = httpContextAccessor.HttpContext?.Items[McpTokenKeys.HttpContextItemKey] as string;
        if (!string.IsNullOrEmpty(token))
            request.Headers.TryAddWithoutValidation("X-Mcp-Token", token);

        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>Shared constants for MCP token propagation.</summary>
internal static class McpTokenKeys
{
    /// <summary>Key used in <see cref="HttpContext.Items"/> to store the raw bearer token.</summary>
    public const string HttpContextItemKey = "McpBearerToken";
}
