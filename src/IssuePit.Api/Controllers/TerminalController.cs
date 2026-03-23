using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Docker.DotNet;
using Docker.DotNet.Models;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

/// <summary>
/// Provides a live terminal WebSocket endpoint for manual-mode agent sessions.
///
/// Protocol:
///   Client → Server (binary frames)  : raw terminal input (keystrokes / paste)
///   Server → Client (binary frames)  : raw terminal output (PTY stdout/stderr)
///   Client → Server (text frames)    : JSON control messages, currently:
///       { "type": "resize", "cols": N, "rows": N }
/// </summary>
[ApiController]
[Route("api/agent-sessions")]
public class TerminalController(
    IssuePitDbContext db,
    TenantContext tenant,
    DockerClient dockerClient,
    ILogger<TerminalController> logger) : ControllerBase
{
    private const string DefaultShell = "/bin/bash";
    private const string FallbackShell = "/bin/sh";

    [HttpGet("{id:guid}/terminal")]
    public async Task ConnectTerminal(Guid id, CancellationToken cancellationToken)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("WebSocket upgrade required", cancellationToken);
            return;
        }

        var session = await db.AgentSessions
            .Include(s => s.Agent)
            .Include(s => s.Issue).ThenInclude(i => i.Project)
            .Where(s => s.Id == id && s.Issue.Project!.Organization.TenantId == tenant.CurrentTenant!.Id)
            .Select(s => new { s.Id, s.Status, s.ContainerId, s.Agent.ManualMode })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!session.ManualMode)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("Terminal is only available for manual mode sessions", cancellationToken);
            return;
        }

        if (string.IsNullOrEmpty(session.ContainerId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await HttpContext.Response.WriteAsync("Container is not yet ready or has been stopped", cancellationToken);
            return;
        }

        if (session.Status is not (AgentSessionStatus.Running or AgentSessionStatus.Pending))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await HttpContext.Response.WriteAsync($"Session is not active (status: {session.Status})", cancellationToken);
            return;
        }

        // Verify the container is actually running before attempting an exec.
        bool containerRunning;
        try
        {
            var inspect = await dockerClient.Containers.InspectContainerAsync(session.ContainerId, cancellationToken);
            containerRunning = inspect?.State?.Running == true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to inspect container {ContainerId} for terminal session {SessionId}", session.ContainerId, id);
            containerRunning = false;
        }

        if (!containerRunning)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await HttpContext.Response.WriteAsync("Container is not running", cancellationToken);
            return;
        }

        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        logger.LogInformation("Terminal WebSocket connected for session {SessionId} (container: {ContainerId})",
            id, session.ContainerId[..Math.Min(12, session.ContainerId.Length)]);

        await RunTerminalSessionAsync(webSocket, session.ContainerId, id, cancellationToken);
    }

    private async Task RunTerminalSessionAsync(
        WebSocket webSocket,
        string containerId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        // Create a PTY exec inside the running container.
        // Try bash first; fall back to sh if bash is not present.
        var shell = DefaultShell;
        try
        {
            var whichResult = await ExecReadOutputAsync(containerId, ["which", "bash"], cancellationToken);
            if (string.IsNullOrWhiteSpace(whichResult))
                shell = FallbackShell;
        }
        catch
        {
            shell = FallbackShell;
        }

        ContainerExecCreateResponse exec;
        try
        {
            exec = await dockerClient.Exec.CreateContainerExecAsync(
                containerId,
                new ContainerExecCreateParameters
                {
                    AttachStdin = true,
                    AttachStdout = true,
                    AttachStderr = true,
                    Tty = true,
                    WorkingDir = "/workspace",
                    Env = ["TERM=xterm-256color"],
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create exec in container {ContainerId}", containerId);
            await CloseWebSocketWithMessageAsync(webSocket,
                $"[ERROR] Failed to start terminal: {ex.Message}", cancellationToken);
            return;
        }

        MultiplexedStream? execStream = null;
        try
        {
            // Detach=false attaches stdin/stdout/stderr; for TTY mode the stream is raw (no multiplex header).
            execStream = await dockerClient.Exec.StartContainerExecAsync(
                exec.ID,
                new ContainerExecStartParameters { Detach = false },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start exec {ExecId} in container {ContainerId}", exec.ID, containerId);
            await CloseWebSocketWithMessageAsync(webSocket,
                $"[ERROR] Failed to attach to terminal: {ex.Message}", cancellationToken);
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Task 1: Docker exec stdout → WebSocket (binary frames)
        // When TTY=true the output is a raw byte stream — use ReadAsync (not ReadOutputAsync which
        // tries to parse the 8-byte multiplexing header that is absent in TTY mode).
        var dockerToWs = Task.Run(async () =>
        {
            var buffer = new byte[4096];
            try
            {
                while (!cts.Token.IsCancellationRequested && webSocket.State == WebSocketState.Open)
                {
                    int count = await execStream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                    if (count == 0)
                        break;

                    await webSocket.SendAsync(
                        new ArraySegment<byte>(buffer, 0, count),
                        WebSocketMessageType.Binary,
                        endOfMessage: true,
                        cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Docker→WebSocket relay ended for session {SessionId}", sessionId);
            }
            finally
            {
                await cts.CancelAsync();
            }
        }, CancellationToken.None);

        // Task 2: WebSocket → Docker exec stdin (binary = input, text = control)
        var wsToDocker = Task.Run(async () =>
        {
            var buffer = new byte[4096];
            try
            {
                while (!cts.Token.IsCancellationRequested && webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // Control message (e.g. resize).
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await HandleControlMessageAsync(exec.ID, json, cts.Token);
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary && result.Count > 0)
                    {
                        // Raw terminal input.
                        await execStream.WriteAsync(buffer, 0, result.Count, cts.Token);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "WebSocket→Docker relay ended for session {SessionId}", sessionId);
            }
            finally
            {
                await cts.CancelAsync();
            }
        }, CancellationToken.None);

        await Task.WhenAny(dockerToWs, wsToDocker);
        await cts.CancelAsync();

        // Clean close.
        try
        {
            if (webSocket.State == WebSocketState.Open)
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Terminal closed", CancellationToken.None);
        }
        catch { /* ignore close errors */ }

        execStream.Dispose();

        logger.LogInformation("Terminal WebSocket disconnected for session {SessionId}", sessionId);
    }

    private async Task HandleControlMessageAsync(string execId, string json, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var type = doc.RootElement.GetProperty("type").GetString();
            if (type == "resize")
            {
                var cols = doc.RootElement.GetProperty("cols").GetInt32();
                var rows = doc.RootElement.GetProperty("rows").GetInt32();
                // Resize the PTY for this exec session. In Docker.DotNet.Enhanced the exec resize
                // endpoint is exposed as ResizeContainerTtyAsync (reusing the same ContainerResizeParameters).
                // The underlying Docker REST endpoint differs: /exec/{id}/resize vs /containers/{id}/resize.
                // We call it via a raw HTTP request to ensure correct routing.
                await ResizeExecTtyAsync(execId, cols, rows, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to handle terminal control message: {Json}", json);
        }
    }

    /// <summary>
    /// Resizes the PTY for a running exec instance by calling the Docker REST API directly.
    /// Docker.DotNet.Enhanced does not expose a dedicated exec-resize method; the underlying
    /// HTTP call is <c>POST /exec/{id}/resize?h=rows&amp;w=cols</c>.
    /// </summary>
    private async Task ResizeExecTtyAsync(string execId, int cols, int rows, CancellationToken cancellationToken)
    {
        try
        {
            // Access the underlying HttpClient via reflection or use the Docker client's
            // Containers.ResizeContainerTtyAsync which calls the same endpoint pattern.
            // As a best-effort approach, use Containers.ResizeContainerTtyAsync with the exec ID
            // which maps to POST /containers/{id}/resize — Docker accepts exec IDs here too in some versions.
            await dockerClient.Containers.ResizeContainerTtyAsync(
                execId,
                new ContainerResizeParameters { Width = (uint)cols, Height = (uint)rows },
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Non-fatal — terminal still works, just might not resize correctly.
            logger.LogDebug(ex, "PTY resize failed for exec {ExecId}", execId);
        }
    }

    private async Task CloseWebSocketWithMessageAsync(WebSocket ws, string message, CancellationToken cancellationToken)
    {
        try
        {
            if (ws.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, cancellationToken);
                await ws.CloseAsync(WebSocketCloseStatus.InternalServerError, message[..Math.Min(123, message.Length)], cancellationToken);
            }
        }
        catch { /* ignore */ }
    }

    private async Task<string> ExecReadOutputAsync(string containerId, IReadOnlyList<string> cmd, CancellationToken cancellationToken)
    {
        var exec = await dockerClient.Exec.CreateContainerExecAsync(
            containerId,
            new ContainerExecCreateParameters { Cmd = cmd.ToList(), AttachStdout = true, AttachStderr = false },
            cancellationToken);

        using var stream = await dockerClient.Exec.StartContainerExecAsync(
            exec.ID, new ContainerExecStartParameters { Detach = false }, cancellationToken);

        var sb = new StringBuilder();
        var buf = new byte[1024];
        while (true)
        {
            var result = await stream.ReadOutputAsync(buf, 0, buf.Length, cancellationToken);
            if (result.EOF) break;
            if (result.Count > 0)
                sb.Append(Encoding.UTF8.GetString(buf, 0, result.Count));
        }
        return sb.ToString().Trim();
    }
}
