using System.Collections.Concurrent;

namespace IssuePit.McpServer.Tools;

public sealed class McpIssueChangeReviewQueue
{
    private readonly ConcurrentDictionary<Guid, PendingIssueChange> items = new();

    public PendingIssueChange EnqueueCreate(Guid projectId, object payload)
        => Enqueue("CreateIssue", projectId, null, current: null, payload);

    public PendingIssueChange EnqueueUpdate(Guid issueId, object? current, object payload)
        => Enqueue("UpdateIssue", null, issueId, current, payload);

    public PendingIssueChange EnqueueDelete(Guid issueId, object? current)
        => Enqueue("DeleteIssue", null, issueId, current, payload: null);

    public IReadOnlyList<PendingIssueChange> List() =>
        items.Values.OrderBy(x => x.CreatedAt).ToList();

    public bool TryGet(Guid id, out PendingIssueChange? change) =>
        items.TryGetValue(id, out change);

    public bool TryRemove(Guid id, out PendingIssueChange? change) =>
        items.TryRemove(id, out change);

    private PendingIssueChange Enqueue(string toolName, Guid? projectId, Guid? issueId, object? current, object? payload)
    {
        var entry = new PendingIssueChange
        {
            Id = Guid.NewGuid(),
            ToolName = toolName,
            ProjectId = projectId,
            IssueId = issueId,
            CreatedAt = DateTime.UtcNow,
            Current = current,
            Payload = payload
        };
        items[entry.Id] = entry;
        return entry;
    }
}

public sealed class PendingIssueChange
{
    public Guid Id { get; init; }
    public required string ToolName { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? IssueId { get; init; }
    public object? Current { get; init; }
    public object? Payload { get; init; }
}
