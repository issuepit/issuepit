using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.ExecutionClient.Services;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class SessionSummaryBuilderTests
{
    private static AgentSession MakeSession(
        AgentSessionStatus status = AgentSessionStatus.Succeeded,
        string? branch = "feature/fix-bug",
        string? commitSha = "abc123def456") =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Status = status,
            StartedAt = new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            EndedAt = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc),
            GitBranch = branch,
            CommitSha = commitSha,
        };

    private static AgentSessionLog MakeLog(
        Guid sessionId,
        string line,
        LogStream stream = LogStream.Stdout,
        AgentLogSection? section = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            AgentSessionId = sessionId,
            Line = line,
            Stream = stream,
            Section = section,
            Timestamp = DateTime.UtcNow,
        };

    [Fact]
    public void Build_SucceededSession_ContainsOverviewAndStatus()
    {
        var session = MakeSession(AgentSessionStatus.Succeeded);
        var logs = new List<AgentSessionLog>();
        var (title, content) = SessionSummaryBuilder.Build(session, "CodeAgent", "Fix the bug", logs);

        Assert.Contains("Succeeded", title);
        Assert.Contains("CodeAgent", title);
        Assert.Contains("Fix the bug", title);
        Assert.Contains("## Session Overview", content);
        Assert.Contains("**Status**: Succeeded", content);
        Assert.Contains("**Agent**: CodeAgent", content);
        Assert.Contains("**Issue**: Fix the bug", content);
        Assert.Contains("**Branch**: `feature/fix-bug`", content);
        Assert.Contains("completed successfully", content);
    }

    [Fact]
    public void Build_FailedSession_ContainsFailureGuidelines()
    {
        var session = MakeSession(AgentSessionStatus.Failed);
        var logs = new List<AgentSessionLog>
        {
            MakeLog(session.Id, "[ERROR] Build failed: missing dependency", LogStream.Stderr),
        };
        var (title, content) = SessionSummaryBuilder.Build(session, "CodeAgent", "Fix the bug", logs);

        Assert.Contains("Failed", title);
        Assert.Contains("## Errors Encountered", content);
        Assert.Contains("missing dependency", content);
        Assert.Contains("session failed", content);
    }

    [Fact]
    public void Build_WithErrorLogs_ExtractsErrors()
    {
        var session = MakeSession(AgentSessionStatus.Failed);
        var logs = new List<AgentSessionLog>
        {
            MakeLog(session.Id, "[INFO] Starting agent run", LogStream.Stdout),
            MakeLog(session.Id, "[ERROR] Compilation error: missing semicolon", LogStream.Stdout),
            MakeLog(session.Id, "[INFO] Build completed successfully", LogStream.Stdout),
        };
        var (_, content) = SessionSummaryBuilder.Build(session, "Agent", "Issue", logs);

        Assert.Contains("## Errors Encountered", content);
        Assert.Contains("missing semicolon", content);
    }

    [Fact]
    public void Build_NoErrors_OmitsErrorSection()
    {
        var session = MakeSession(AgentSessionStatus.Succeeded);
        var logs = new List<AgentSessionLog>
        {
            MakeLog(session.Id, "[INFO] All tests passed", LogStream.Stdout),
        };
        var (_, content) = SessionSummaryBuilder.Build(session, "Agent", "Issue", logs);

        Assert.DoesNotContain("## Errors Encountered", content);
    }

    [Fact]
    public void Build_WithSignificantActions_ExtractsKeyActions()
    {
        var session = MakeSession(AgentSessionStatus.Succeeded);
        var logs = new List<AgentSessionLog>
        {
            MakeLog(session.Id, "[INFO] CI/CD run completed successfully", LogStream.Stdout),
            MakeLog(session.Id, "[INFO] Push triggered to remote", LogStream.Stdout),
        };
        var (_, content) = SessionSummaryBuilder.Build(session, "Agent", "Issue", logs);

        Assert.Contains("## Key Actions", content);
        Assert.Contains("completed successfully", content);
    }

    [Fact]
    public void Build_IncludesDuration()
    {
        var session = MakeSession();
        var (_, content) = SessionSummaryBuilder.Build(session, "Agent", "Issue", []);

        Assert.Contains("**Duration**: 30.0 minutes", content);
    }

    [Fact]
    public void Build_TitleTruncatedAt500Chars()
    {
        var session = MakeSession();
        var longTitle = new string('A', 600);
        var (title, _) = SessionSummaryBuilder.Build(session, "Agent", longTitle, []);

        Assert.True(title.Length <= 500);
    }
}
