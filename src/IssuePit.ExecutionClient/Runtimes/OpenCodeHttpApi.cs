using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>
/// Implements <see cref="IAgentHttpApi"/> for the opencode agent tool
/// (https://opencode.ai/docs).
///
/// opencode exposes an HTTP server that is started when you run the <c>opencode</c>
/// command without the <c>run</c> subcommand. The server listens on port
/// <see cref="DefaultPort"/> by default (configurable via the <c>OPENCODE_PORT</c>
/// environment variable or the <c>port</c> key in <c>~/.config/opencode/config.json</c>).
///
/// API surface used by this client:
/// <list type="bullet">
///   <item><c>GET  /v1/session</c>       — list all sessions</item>
///   <item><c>POST /v1/session</c>       — create a new session</item>
///   <item><c>GET  /v1/session/{id}</c>  — get session details and status</item>
///   <item><c>POST /v1/session/{id}/message</c> — send a message (task) to a session</item>
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

    // opencode session status values returned by GET /v1/session/{id}
    private const string SessionStatusCompleted = "completed";
    private const string SessionStatusIdle = "idle";   // also a terminal state (no more messages expected)
    private const string SessionStatusError = "error";

    /// <inheritdoc/>
    public async Task<bool> IsReadyAsync(string serverBaseUrl, CancellationToken cancellationToken)
    {
        try
        {
            // The session list endpoint is lightweight and always available when the server is up.
            var response = await httpClient.GetAsync($"{serverBaseUrl}/v1/session", cancellationToken);
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
            var response = await httpClient.GetAsync($"{serverBaseUrl}/v1/session", cancellationToken);
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
            $"{serverBaseUrl}/v1/session",
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
        var response = await httpClient.PostAsync(
            $"{serverBaseUrl}/v1/session/{sessionId}/message",
            JsonContent.Create(new { role = "user", content = message }),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted)
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

        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(PollInterval, cancellationToken);

            JsonElement sessionData;
            try
            {
                var response = await httpClient.GetAsync(
                    $"{serverBaseUrl}/v1/session/{sessionId}",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Polling opencode session {SessionId} returned {Status}", sessionId, response.StatusCode);
                    continue;
                }

                sessionData = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Polling error for opencode session {SessionId}, will retry", sessionId);
                continue;
            }

            // Emit any new messages from the session as log lines.
            if (sessionData.TryGetProperty("messages", out var messages))
            {
                var messageArray = messages.EnumerateArray().ToList();
                for (var i = lastMessageCount; i < messageArray.Count; i++)
                {
                    var msg = messageArray[i];
                    var role = msg.TryGetProperty("role", out var roleEl) ? roleEl.GetString() : "?";
                    var text = ExtractMessageText(msg);
                    if (!string.IsNullOrWhiteSpace(text))
                        await onLogLine($"[opencode] [{role}] {text}");
                }
                lastMessageCount = messageArray.Count;
            }

            // Check the session status.
            var status = sessionData.TryGetProperty("status", out var statusEl)
                ? statusEl.GetString()
                : null;

            logger.LogDebug("opencode session {SessionId} status: {Status}", sessionId, status);

            if (status == SessionStatusCompleted || status == SessionStatusIdle)
                return AgentHttpSessionStatus.Completed;
            if (status == SessionStatusError)
                return AgentHttpSessionStatus.Error;
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
        var response = await httpClient.GetAsync($"{serverBaseUrl}/v1/session", cancellationToken);
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

    /// <summary>Extracts displayable text from an opencode message element.</summary>
    private static string? ExtractMessageText(JsonElement message)
    {
        // opencode messages have a "content" field that may be a string or an array of parts.
        if (!message.TryGetProperty("content", out var content))
            return null;

        if (content.ValueKind == JsonValueKind.String)
            return content.GetString();

        if (content.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "text"
                    && part.TryGetProperty("text", out var textEl))
                {
                    var text = textEl.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                        parts.Add(text);
                }
            }
            return parts.Count > 0 ? string.Join("\n", parts) : null;
        }

        return null;
    }
}
