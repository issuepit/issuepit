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

    [Fact]
    public void Issue_ExternalId_DefaultsToNull()
    {
        var issue = new Issue();
        Assert.Null(issue.ExternalId);
    }

    [Fact]
    public void Issue_ExternalSourceId_DefaultsToNull()
    {
        var issue = new Issue();
        Assert.Null(issue.ExternalSourceId);
    }

    [Fact]
    public void Issue_ExternalSource_NavigationProperty_DefaultsToNull()
    {
        var issue = new Issue();
        Assert.Null(issue.ExternalSource);
    }

    [Fact]
    public void Issue_CanSetExternalIdAndSource()
    {
        var sourceId = Guid.NewGuid();
        var source = new IssueExternalSource { Id = sourceId, Type = "github", ProjectId = Guid.NewGuid() };
        var issue = new Issue { ExternalId = 42, ExternalSourceId = sourceId, ExternalSource = source };
        Assert.Equal(42, issue.ExternalId);
        Assert.Equal(sourceId, issue.ExternalSourceId);
        Assert.Equal("github", issue.ExternalSource!.Type);
    }
}
