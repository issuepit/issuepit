using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>
/// Implements <see cref="IAgentHttpApi"/> for the opencode agent tool
/// (https://opencode.ai/docs).
///
/// The server is started with <c>opencode serve --hostname 0.0.0.0 --port 4096</c>.
/// The default hostname is <c>127.0.0.1</c> (loopback only); <c>--hostname 0.0.0.0</c>
/// is required so Docker port-mapping can reach the server from the host.
///
/// API surface used by this client (no <c>/v1/</c> prefix in opencode's REST API):
/// <list type="bullet">
///   <item><c>GET  /global/health</c>              — server readiness / health check</item>
///   <item><c>GET  /session</c>                    — list all sessions</item>
///   <item><c>POST /session</c>                    — create a new session</item>
///   <item><c>POST /session/{id}/prompt_async</c>  — send a task message (fire-and-forget, 204)</item>
///   <item><c>GET  /session/status</c>             — get status map for all sessions</item>
///   <item><c>GET  /session/{id}/message</c>       — list messages in a session</item>
/// </list>
/// </summary>
public class OpenCodeHttpApi(HttpClient httpClient, ILogger<OpenCodeHttpApi> logger) : IAgentHttpApi
{
    /// <summary>Default port the opencode server listens on.</summary>
    public const int DefaultPort = 4096;

    /// <summary>Polling interval when waiting for a session to complete.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    /// <summary>Maximum time to wait for a session to reach a terminal state.</summary>
    private static readonly TimeSpan MaxWaitTime = TimeSpan.FromMinutes(30);

    // opencode session status type values returned by GET /session/status
    // See: https://opencode.ai/docs/server/
    private const string SessionStatusIdle = "idle";   // session is done / no active request
    private const string SessionStatusBusy = "busy";   // session is actively processing

    /// <inheritdoc/>
    public async Task<bool> IsReadyAsync(string serverBaseUrl, CancellationToken cancellationToken)
    {
        try
        {
            // Use the dedicated health endpoint: GET /global/health → { healthy: true, version: "..." }
            var response = await httpClient.GetAsync($"{serverBaseUrl}/global/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<string?> GetServerInfoAsync(string serverBaseUrl, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetAsync($"{serverBaseUrl}/session", cancellationToken);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync(cancellationToken);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "GetServerInfoAsync failed for {BaseUrl}", serverBaseUrl);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<string> CreateSessionAsync(string serverBaseUrl, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsync(
            $"{serverBaseUrl}/session",
            JsonContent.Create(new { }),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var sessionId = content.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("opencode session creation response did not contain 'id'.");

        logger.LogDebug("Created opencode session {SessionId} on {BaseUrl}", sessionId, serverBaseUrl);
        return sessionId;
    }

    /// <inheritdoc/>
    public async Task SendMessageAsync(
        string serverBaseUrl,
        string sessionId,
        string message,
        CancellationToken cancellationToken)
    {
        // Use prompt_async (fire-and-forget, returns 204 No Content immediately).
        // The session processes the message in the background; WaitForCompletionAsync polls for completion.
        // Body format: { parts: [{ type: "text", text: "<message>" }] }
        var response = await httpClient.PostAsync(
            $"{serverBaseUrl}/session/{sessionId}/prompt_async",
            JsonContent.Create(new { parts = new[] { new { type = "text", text = message } } }),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent
            || response.StatusCode == HttpStatusCode.OK
            || response.StatusCode == HttpStatusCode.Accepted)
        {
            logger.LogDebug("Sent message to opencode session {SessionId}", sessionId);
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task<AgentHttpSessionStatus> WaitForCompletionAsync(
        string serverBaseUrl,
        string sessionId,
        Func<string, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(MaxWaitTime);
        var lastMessageCount = 0;
        var seenBusy = false;

        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(PollInterval, cancellationToken);

            string? statusType = null;
            try
            {
                // Poll GET /session/status → { [sessionId]: { type: "idle"|"busy"|"retry" } }
                var statusResponse = await httpClient.GetAsync(
                    $"{serverBaseUrl}/session/status", cancellationToken);

                if (statusResponse.IsSuccessStatusCode)
                {
                    var statusMap = await statusResponse.Content.ReadFromJsonAsync<JsonElement>(
                        cancellationToken: cancellationToken);

                    if (statusMap.TryGetProperty(sessionId, out var sessionStatus)
                        && sessionStatus.TryGetProperty("type", out var typeEl))
                    {
                        statusType = typeEl.GetString();
                    }
                }
                else
                {
                    logger.LogWarning("GET /session/status returned {Status}", statusResponse.StatusCode);
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Status polling error for opencode session {SessionId}, will retry", sessionId);
                continue;
            }

            logger.LogDebug("opencode session {SessionId} status: {Status}", sessionId, statusType);

            if (statusType == SessionStatusBusy)
                seenBusy = true;

            // Fetch and emit any new messages as log lines.
            try
            {
                var messagesResponse = await httpClient.GetAsync(
                    $"{serverBaseUrl}/session/{sessionId}/message", cancellationToken);

                if (messagesResponse.IsSuccessStatusCode)
                {
                    var messageItems = await messagesResponse.Content.ReadFromJsonAsync<JsonElement>(
                        cancellationToken: cancellationToken);

                    if (messageItems.ValueKind == JsonValueKind.Array)
                    {
                        var messageArray = messageItems.EnumerateArray().ToList();
                        for (var i = lastMessageCount; i < messageArray.Count; i++)
                        {
                            var item = messageArray[i];
                            var role = item.TryGetProperty("info", out var info)
                                && info.TryGetProperty("role", out var roleEl)
                                ? roleEl.GetString() : "?";
                            var text = ExtractPartsText(item);
                            if (!string.IsNullOrWhiteSpace(text))
                                await onLogLine($"[opencode] [{role}] {text}");
                        }
                        lastMessageCount = messageArray.Count;
                    }
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Message polling error for opencode session {SessionId}", sessionId);
            }

            // Only consider "idle" as terminal after the agent has started processing ("busy").
            // This prevents a false-positive on the initial idle state before the agent picks up the task.
            if (statusType == SessionStatusIdle && seenBusy)
            {
                // Check if the last assistant message contains an error.
                try
                {
                    var messagesResponse = await httpClient.GetAsync(
                        $"{serverBaseUrl}/session/{sessionId}/message", cancellationToken);

                    if (messagesResponse.IsSuccessStatusCode)
                    {
                        var messageItems = await messagesResponse.Content.ReadFromJsonAsync<JsonElement>(
                            cancellationToken: cancellationToken);

                        if (messageItems.ValueKind == JsonValueKind.Array)
                        {
                            // Walk messages in reverse to find the last assistant message.
                            foreach (var item in messageItems.EnumerateArray().Reverse())
                            {
                                if (!item.TryGetProperty("info", out var info)) continue;
                                if (!info.TryGetProperty("role", out var roleEl)) continue;
                                if (roleEl.GetString() != "assistant") continue;

                                // If the assistant message has a non-null error property, report failure.
                                if (info.TryGetProperty("error", out var errorEl)
                                    && errorEl.ValueKind != JsonValueKind.Null
                                    && errorEl.ValueKind != JsonValueKind.Undefined)
                                {
                                    logger.LogWarning(
                                        "opencode session {SessionId} finished with an error: {Error}",
                                        sessionId, errorEl);
                                    return AgentHttpSessionStatus.Error;
                                }
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogDebug(ex, "Error checking final message for opencode session {SessionId}", sessionId);
                }

                return AgentHttpSessionStatus.Completed;
            }
        }

        if (cancellationToken.IsCancellationRequested)
            return AgentHttpSessionStatus.TimedOut;

        logger.LogWarning("Timed out waiting for opencode session {SessionId} after {Minutes} minutes",
            sessionId, MaxWaitTime.TotalMinutes);
        return AgentHttpSessionStatus.TimedOut;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListSessionsAsync(string serverBaseUrl, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"{serverBaseUrl}/session", cancellationToken);
        response.EnsureSuccessStatusCode();

        var sessions = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var ids = new List<string>();

        if (sessions.ValueKind == JsonValueKind.Array)
        {
            foreach (var session in sessions.EnumerateArray())
            {
                if (session.TryGetProperty("id", out var idEl) && idEl.GetString() is string id)
                    ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>
    /// Extracts displayable text from a message item returned by <c>GET /session/{id}/message</c>.
    /// Each item has shape <c>{ info: Message, parts: Part[] }</c>. We concatenate all text parts.
    /// </summary>
    private static string? ExtractPartsText(JsonElement messageItem)
    {
        if (!messageItem.TryGetProperty("parts", out var parts))
            return null;

        if (parts.ValueKind != JsonValueKind.Array)
            return null;

        var textParts = new List<string>();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "text"
                && part.TryGetProperty("text", out var textEl))
            {
                var text = textEl.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    textParts.Add(text);
            }
        }

        return textParts.Count > 0 ? string.Join("\n", textParts) : null;
    }
}
