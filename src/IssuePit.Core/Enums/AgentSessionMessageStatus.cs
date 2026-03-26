namespace IssuePit.Core.Enums;

/// <summary>Lifecycle status of a queued user message in an agent session.</summary>
public enum AgentSessionMessageStatus
{
    /// <summary>Message is waiting to be processed.</summary>
    Pending = 1,

    /// <summary>Message is currently being processed by the agent.</summary>
    Running = 2,

    /// <summary>Message was processed successfully.</summary>
    Done = 3,

    /// <summary>Message was cancelled before it could be processed.</summary>
    Cancelled = 4,
}
