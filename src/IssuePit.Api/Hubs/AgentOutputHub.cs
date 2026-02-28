using Microsoft.AspNetCore.SignalR;

namespace IssuePit.Api.Hubs;

/// <summary>
/// Streams live agent execution output (stdout/stderr lines) to connected clients.
/// Clients join a group per issue to receive output only for the relevant task.
/// </summary>
public class AgentOutputHub : Hub
{
    public async Task JoinIssue(string issueId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, IssueGroup(issueId));

    public async Task LeaveIssue(string issueId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, IssueGroup(issueId));

    public static string IssueGroup(string issueId) => $"issue:{issueId}";
}
