using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Docker.DotNet;
using Docker.DotNet.Models;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.TerminalServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.TerminalServer.Controllers;

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
    private const string TmuxSessionName = "main";

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
            .Include(s => s.Project).ThenInclude(p => p!.Organization)
            .Where(s => s.Id == id && s.Project!.Organization.TenantId == tenant.CurrentTenant!.Id)
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
        // Prefer tmux so that the shell session survives page reloads.
        // "tmux new-session -A -s <name>" attaches to an existing session named <name>
        // or creates a new one if none exists — giving persistent session semantics.
        // Fall back to bash / sh when tmux is not installed.
        bool hasTmux;
        try
        {
            var tmuxPath = await ExecReadOutputAsync(containerId, ["which", "tmux"], cancellationToken);
            hasTmux = !string.IsNullOrWhiteSpace(tmuxPath);
        }
        catch
        {
            hasTmux = false;
        }

        List<string> cmd;
        if (hasTmux)
        {
            cmd = ["tmux", "new-session", "-A", "-s", TmuxSessionName];
        }
        else
        {
            // tmux is not available — warn the user so they know the session will not persist across
            // page reloads.  The standard helper image ships tmux; this path indicates a custom image
            // that does not include it.
            logger.LogWarning("tmux not found in container {ContainerId} for session {SessionId}. " +
                "Terminal session will not persist across page reloads. " +
                "Install tmux in the container image to enable session persistence.",
                containerId[..Math.Min(12, containerId.Length)], sessionId);

            // Send a visible warning line to the terminal before the shell starts.
            const string noTmuxWarning =
                "\r\n\x1b[33m⚠  tmux not found in this container image. " +
                "Terminal session will not persist across page reloads.\r\n" +
                "   Install tmux in your Docker image to enable session persistence.\x1b[0m\r\n\r\n";
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    var warningBytes = Encoding.UTF8.GetBytes(noTmuxWarning);
                    await webSocket.SendAsync(new ArraySegment<byte>(warningBytes),
                        WebSocketMessageType.Binary, true, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                // Best-effort — continue to start the shell even if the warning couldn't be sent.
                logger.LogDebug(ex, "Failed to send tmux-absent warning to terminal for session {SessionId}", sessionId);
            }

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
            cmd = [shell];
        }

        ContainerExecCreateResponse exec;
        try
        {
            exec = await dockerClient.Exec.CreateContainerExecAsync(
                containerId,
                new ContainerExecCreateParameters
                {
                    Cmd = cmd,
                    AttachStdin = true,
                    AttachStdout = true,
                    AttachStderr = true,
                    TTY = true,
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
                    // In TTY mode the output is a raw byte stream — ReadOutputAsync reads it
                    // treating all bytes as stdout (no multiplex header is present with TTY=true).
                    var readResult = await execStream.ReadOutputAsync(buffer, 0, buffer.Length, cts.Token);
                    if (readResult.EOF)
                        break;
                    int count = (int)readResult.Count;

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
            // Best-effort: use Containers.ResizeContainerTtyAsync with the exec ID.
            // Docker accepts exec IDs on the container resize endpoint in some versions.
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
                sb.Append(Encoding.UTF8.GetString(buf, 0, (int)result.Count));
        }
        return sb.ToString().Trim();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // auth.json backup/restore
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the opencode <c>auth.json</c> file from the running container and saves it to the
    /// database as an <see cref="AgentAuth"/> record.  Call this from the live terminal after
    /// authenticating with GitHub (or another provider) inside the container so the credentials
    /// can be reused by autonomous agent runs later.
    ///
    /// The file is read from <c>~/.local/share/opencode/auth.json</c> (the default opencode
    /// credentials location). Returns 404 if the file does not exist yet.
    /// </summary>
    [HttpPost("{id:guid}/auth-json/backup")]
    public async Task<IActionResult> BackupAuthJson(Guid id, [FromBody] BackupAuthJsonRequest request, CancellationToken cancellationToken)
    {
        var session = await db.AgentSessions
            .Include(s => s.Agent)
            .Include(s => s.Project).ThenInclude(p => p!.Organization)
            .Where(s => s.Id == id && s.Project!.Organization.TenantId == tenant.CurrentTenant!.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null) return NotFound();

        if (!session.Agent.ManualMode)
            return BadRequest(new { error = "Auth backup is only available for manual-mode sessions." });

        if (string.IsNullOrEmpty(session.ContainerId))
            return Conflict(new { error = "Container is not yet ready or has been stopped." });

        // Verify container is running.
        try
        {
            var inspect = await dockerClient.Containers.InspectContainerAsync(session.ContainerId, cancellationToken);
            if (inspect?.State?.Running != true)
                return Conflict(new { error = "Container is not running." });
        }
        catch
        {
            return Conflict(new { error = "Container is not accessible." });
        }

        // Read auth.json from the container.
        const string authPath = "/root/.local/share/opencode/auth.json";
        string authContent;
        try
        {
            authContent = await ExecReadOutputAsync(session.ContainerId, ["cat", authPath], cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read auth.json from container {ContainerId}", session.ContainerId[..Math.Min(12, session.ContainerId.Length)]);
            return NotFound(new { error = "auth.json not found in container. Authenticate first (e.g. run opencode and log in to GitHub)." });
        }

        if (string.IsNullOrWhiteSpace(authContent) || authContent.Length < 10)
            return NotFound(new { error = "auth.json is empty or does not exist. Authenticate first." });

        var orgId = session.Project!.OrgId;
        var tenantId = tenant.CurrentTenant!.Id;

        var label = string.IsNullOrWhiteSpace(request.Label)
            ? $"auth backup from session {id.ToString()[..8]} on {DateTime.UtcNow:yyyy-MM-dd}"
            : request.Label;

        var auth = new AgentAuth
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrgId = orgId,
            AgentSessionId = id,
            Label = label,
            AuthJsonContent = authContent,
            RestoreOnAgentRuns = request.RestoreOnAgentRuns,
            CapturedAt = DateTime.UtcNow,
        };

        db.AgentAuths.Add(auth);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("auth.json backed up from session {SessionId} as AgentAuth {AuthId}", id, auth.Id);
        return Ok(new { auth.Id, auth.Label, auth.CapturedAt, auth.RestoreOnAgentRuns });
    }
}

public record BackupAuthJsonRequest(
    string? Label = null,
    bool RestoreOnAgentRuns = false);
