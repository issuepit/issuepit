using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class IssueEntityTests
{
    [Fact]
    public void Issue_DefaultStatus_IsBacklog()
    {
        var issue = new Issue();
        Assert.Equal(IssueStatus.Backlog, issue.Status);
    }

    [Fact]
    public void Issue_DefaultPriority_IsNoPriority()
    {
        var issue = new Issue();
        Assert.Equal(IssuePriority.NoPriority, issue.Priority);
    }

    [Fact]
    public void Issue_DefaultType_IsIssue()
    {
        var issue = new Issue();
        Assert.Equal(IssueType.Issue, issue.Type);
    }

    [Fact]
    public void Issue_SubIssues_InitialisedEmpty()
    {
        var issue = new Issue();
        Assert.Empty(issue.SubIssues);
    }

    [Fact]
    public void Issue_Labels_InitialisedEmpty()
    {
        var issue = new Issue();
        Assert.Empty(issue.Labels);
    }

    [Fact]
    public void Issue_Assignees_InitialisedEmpty()
    {
        var issue = new Issue();
        Assert.Empty(issue.Assignees);
    }
}
