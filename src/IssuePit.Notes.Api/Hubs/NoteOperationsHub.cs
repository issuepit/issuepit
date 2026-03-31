using Microsoft.AspNetCore.SignalR;

namespace IssuePit.Notes.Api.Hubs;

/// <summary>
/// SignalR hub for real-time CRDT operation delivery.
/// Clients join a group per note and receive operations submitted by other clients.
/// This eliminates the need for polling: when a client submits an operation via
/// <c>POST /api/notes/{id}/operations</c>, the server broadcasts the confirmed operation
/// to all other clients in the note's group immediately.
/// </summary>
public class NoteOperationsHub : Hub
{
    /// <summary>Join the real-time group for a specific note.</summary>
    public async Task JoinNote(string noteId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, NoteGroup(noteId));

    /// <summary>Leave the real-time group for a specific note.</summary>
    public async Task LeaveNote(string noteId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, NoteGroup(noteId));

    public static string NoteGroup(string noteId) => $"note:{noteId}";
}
