namespace IssuePit.Core.Enums;

[Flags]
public enum TelegramNotificationEvent
{
    IssueCreated = 1,
    IssueUpdated = 2,
    IssueAssigned = 4,
    AgentStarted = 8,
    AgentCompleted = 16,
    AgentFailed = 32,
    CiCdFailed = 64,
    CiCdWaitingApproval = 128,
    IssueNeedsTriage = 256,
}
