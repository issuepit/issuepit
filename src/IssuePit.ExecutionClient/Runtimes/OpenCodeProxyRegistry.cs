using System.Collections.Concurrent;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>
/// Registry that maps an agent session ID to the opencode HTTP server URL reachable from
/// the execution client process, and tracks whether the server has been confirmed healthy.
///
/// Used by <see cref="Controllers.OpenCodeProxyController"/> to proxy requests to the
/// opencode server running inside the Docker container. Clients (tests, UI) access the
/// opencode server via the execution client's own HTTP endpoint rather than directly via
/// Docker port-mapping, which eliminates race conditions caused by the container stopping
/// before external clients can complete their requests.
/// </summary>
public interface IOpenCodeProxyRegistry
{
    /// <summary>
    /// Registers a session with the proxy registry.
    /// </summary>
    /// <param name="sessionId">The agent session ID.</param>
    /// <param name="serverBaseUrl">
    /// The base URL of the opencode HTTP server, reachable from the execution client process
    /// (e.g. <c>http://localhost:32776</c> or <c>http://172.17.0.1:32776</c>).
    /// </param>
    /// <param name="confirmedHealthy">
    /// Set to <c>true</c> when the server has passed its readiness check. When <c>true</c>,
    /// <see cref="TryGetEntry"/> returns the cached health status so health-check requests can
    /// succeed even after the container has stopped.
    /// </param>
    void Register(Guid sessionId, string serverBaseUrl, bool confirmedHealthy = false);

    /// <summary>
    /// Retrieves the registration entry for a session.
    /// Returns <c>false</c> when no entry is found.
    /// </summary>
    bool TryGetEntry(Guid sessionId, out string? serverBaseUrl, out bool confirmedHealthy);
}

/// <inheritdoc/>
public sealed class OpenCodeProxyRegistry : IOpenCodeProxyRegistry
{
    private readonly ConcurrentDictionary<Guid, (string ServerBaseUrl, bool ConfirmedHealthy)> _entries = new();

    /// <inheritdoc/>
    public void Register(Guid sessionId, string serverBaseUrl, bool confirmedHealthy = false)
        => _entries[sessionId] = (serverBaseUrl, confirmedHealthy);

    /// <inheritdoc/>
    public bool TryGetEntry(Guid sessionId, out string? serverBaseUrl, out bool confirmedHealthy)
    {
        if (_entries.TryGetValue(sessionId, out var entry))
        {
            serverBaseUrl = entry.ServerBaseUrl;
            confirmedHealthy = entry.ConfirmedHealthy;
            return true;
        }

        serverBaseUrl = null;
        confirmedHealthy = false;
        return false;
    }
}
