using Microsoft.AspNetCore.SignalR;

namespace IssuePit.Api.Hubs;

/// <summary>
/// Pushes real-time project-level events (run status changes, etc.) to connected clients.
/// Clients join a group per project to receive events only for that project.
/// </summary>
public class ProjectHub : Hub
{
    public async Task JoinProject(string projectId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, ProjectGroup(projectId));

    public async Task LeaveProject(string projectId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, ProjectGroup(projectId));

    public static string ProjectGroup(string projectId) => $"project:{projectId}";
}
