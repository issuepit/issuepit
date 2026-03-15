namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>
/// Abstraction over an agent tool's built-in HTTP server API.
///
/// When an agent is configured with <c>UseHttpServer = true</c>, the execution client
/// starts the agent's process as a long-running HTTP server and drives all session
/// management through this interface rather than via <c>docker exec</c> CLI invocations.
///
/// The interface is intentionally tool-agnostic so that any CLI agent with a similar
/// HTTP API (opencode, or future tools) can implement it without touching the Docker
/// runtime infrastructure.
/// </summary>
public interface IAgentHttpApi
{
    /// <summary>
    /// Returns <c>true</c> when the server is reachable and ready to accept API requests.
    /// Used to poll after container start before attempting to create sessions.
    /// </summary>
    Task<bool> IsReadyAsync(string serverBaseUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a JSON string with basic server info (version, uptime, etc.).
    /// Returns <c>null</c> when the endpoint is unavailable or returns an error.
    /// Intended for debug logging only.
    /// </summary>
    Task<string?> GetServerInfoAsync(string serverBaseUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new empty session on the server and returns its session ID.
    /// </summary>
    Task<string> CreateSessionAsync(string serverBaseUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a task message to an existing session, triggering the agent to start processing.
    /// </summary>
    Task SendMessageAsync(string serverBaseUrl, string sessionId, string message, CancellationToken cancellationToken);

    /// <summary>
    /// Polls the session until it transitions to a terminal state (completed or error),
    /// then returns the final status.
    /// Calls <paramref name="onLogLine"/> for each new output line captured during polling.
    /// </summary>
    Task<AgentHttpSessionStatus> WaitForCompletionAsync(
        string serverBaseUrl,
        string sessionId,
        Func<string, Task> onLogLine,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the IDs of all sessions currently tracked by the server.
    /// Useful for diagnostics and for verifying the server is healthy.
    /// </summary>
    Task<IReadOnlyList<string>> ListSessionsAsync(string serverBaseUrl, CancellationToken cancellationToken);
}

/// <summary>Terminal status of an agent HTTP session.</summary>
public enum AgentHttpSessionStatus
{
    /// <summary>The session completed successfully (agent finished the task).</summary>
    Completed,

    /// <summary>The session ended with an error reported by the agent.</summary>
    Error,

    /// <summary>The polling timeout elapsed before the session reached a terminal state.</summary>
    TimedOut,
}
