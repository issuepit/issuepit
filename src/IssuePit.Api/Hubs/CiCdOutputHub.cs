using Microsoft.AspNetCore.SignalR;

namespace IssuePit.Api.Hubs;

/// <summary>
/// Streams live CI/CD run output (stdout/stderr lines) to connected clients.
/// Clients join a group per run to receive output only for that run.
/// Also broadcasts run status changes (pending → running → succeeded/failed).
/// </summary>
public class CiCdOutputHub : Hub
{
    public async Task JoinRun(string runId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, RunGroup(runId));

    public async Task LeaveRun(string runId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, RunGroup(runId));

    public static string RunGroup(string runId) => $"cicd-run:{runId}";
}
