using Microsoft.AspNetCore.SignalR;

namespace IssuePit.Api.Hubs;

/// <summary>
/// Streams live agent execution output (stdout/stderr lines) to connected clients.
/// Clients join a group per session to receive output only for the relevant session.
/// Also supports issue-based groups for backwards compatibility.
/// </summary>
public class AgentOutputHub : Hub
{
    public async Task JoinSession(string sessionId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId));

    public async Task LeaveSession(string sessionId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(sessionId));

    public async Task JoinIssue(string issueId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, IssueGroup(issueId));

    public async Task LeaveIssue(string issueId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, IssueGroup(issueId));

    public static string SessionGroup(string sessionId) => $"agent-session:{sessionId}";
    public static string IssueGroup(string issueId) => $"issue:{issueId}";
}
