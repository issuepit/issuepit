using Microsoft.AspNetCore.SignalR;

namespace IssuePit.Api.Hubs;

/// <summary>
/// Pushes real-time Kanban board updates (card moves, status changes) to connected clients.
/// Clients join a group per project board.
/// </summary>
public class KanbanHub : Hub
{
    public async Task JoinBoard(string projectId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, BoardGroup(projectId));

    public async Task LeaveBoard(string projectId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, BoardGroup(projectId));

    public static string BoardGroup(string projectId) => $"board:{projectId}";
}
