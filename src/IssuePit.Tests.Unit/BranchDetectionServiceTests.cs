using IssuePit.Api.Services;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class BranchDetectionServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<int, Guid> NumberMap(params int[] numbers)
        => numbers.ToDictionary(n => n, _ => Guid.NewGuid());

    private static Dictionary<int, Guid> GitHubMap(params int[] numbers)
        => numbers.ToDictionary(n => n, _ => Guid.NewGuid());

    // ── Branch name extraction ────────────────────────────────────────────────

    [Theory]
    [InlineData("fix/69-something", 69)]
    [InlineData("feat/123-my-feature", 123)]
    [InlineData("bugfix/42", 42)]
    public void ResolveBranchIssueIds_PlainNumber_ReturnsCorrectIssue(string branchName, int expectedNumber)
    {
        var issueId = Guid.NewGuid();
        var byNumber = new Dictionary<int, Guid> { [expectedNumber] = issueId };

        var result = BranchDetectionService.ResolveBranchIssueIds(branchName, byNumber, []);

        var ids = result.ToList();
        Assert.Single(ids);
        Assert.Equal(issueId, ids[0]);
    }

    [Theory]
    [InlineData("feat/ip-123-another")]
    [InlineData("feat/ip123-another-branch")]
    [InlineData("fix/IP-456-some-fix")]
    [InlineData("chore/IP456")]
    public void ResolveBranchIssueIds_IssuePitPrefix_ReturnsCorrectIssue(string branchName)
    {
        var issueId = Guid.NewGuid();
        // Extract expected number from the branch name (the numeric part after ip-? prefix)
        var segment = branchName[(branchName.LastIndexOf('/') + 1)..];
        var numStr = System.Text.RegularExpressions.Regex.Match(segment, @"ip-?(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups[1].Value;
        var num = int.Parse(numStr);

        var byNumber = new Dictionary<int, Guid> { [num] = issueId };

        var result = BranchDetectionService.ResolveBranchIssueIds(branchName, byNumber, []);

        var ids = result.ToList();
        Assert.Single(ids);
        Assert.Equal(issueId, ids[0]);
    }

    [Fact]
    public void ResolveBranchIssueIds_NoMatch_ReturnsEmpty()
    {
        var result = BranchDetectionService.ResolveBranchIssueIds("feat/no-issue-here", NumberMap(), GitHubMap());
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveBranchIssueIds_BranchWithoutSlash_ReturnsEmpty()
    {
        // A branch with no slash and no number → nothing
        var result = BranchDetectionService.ResolveBranchIssueIds("main", NumberMap(), GitHubMap());
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveBranchIssueIds_UnknownNumber_ReturnsEmpty()
    {
        // The number exists in branch but not in the project's issue list
        var result = BranchDetectionService.ResolveBranchIssueIds("fix/99-something", NumberMap(1, 2, 3), GitHubMap());
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveBranchIssueIds_GitHubNumber_ReturnsGitHubLinkedIssue()
    {
        var issueId = Guid.NewGuid();
        var byGitHub = new Dictionary<int, Guid> { [55] = issueId };

        // Branch "fix/55-blah" — no IssuePit issue #55, but a linked GitHub issue #55
        var result = BranchDetectionService.ResolveBranchIssueIds("fix/55-blah", NumberMap(), byGitHub);

        var ids = result.ToList();
        Assert.Single(ids);
        Assert.Equal(issueId, ids[0]);
    }

    [Fact]
    public void ResolveBranchIssueIds_NoBranchSlash_PlainNumberAtStart()
    {
        var issueId = Guid.NewGuid();
        var byNumber = new Dictionary<int, Guid> { [7] = issueId };

        // Branch with no slash; leading number in segment should still match
        var result = BranchDetectionService.ResolveBranchIssueIds("7-hotfix-login", byNumber, GitHubMap());

        var ids = result.ToList();
        Assert.Single(ids);
        Assert.Equal(issueId, ids[0]);
    }

    [Fact]
    public void ResolveBranchIssueIds_TrailingSlash_StillReturnsMatch()
    {
        var issueId = Guid.NewGuid();
        var byNumber = new Dictionary<int, Guid> { [42] = issueId };

        // Trailing slash should not prevent matching
        var result = BranchDetectionService.ResolveBranchIssueIds("fix/42-something/", byNumber, GitHubMap());

        var ids = result.ToList();
        Assert.Single(ids);
        Assert.Equal(issueId, ids[0]);
    }

    // ── Commit message extraction ─────────────────────────────────────────────

    [Theory]
    [InlineData("fix: resolve login issue IP-42")]
    [InlineData("ip-42: fix login")]
    [InlineData("closes ip42 login bug")]
    [InlineData("IP42 — some message")]
    public void ResolveCommitIssueIds_IssuePitKey_ReturnsCorrectIssue(string message)
    {
        var issueId = Guid.NewGuid();
        var byNumber = new Dictionary<int, Guid> { [42] = issueId };

        var result = BranchDetectionService.ResolveCommitIssueIds(message, byNumber, []);

        var ids = result.ToList();
        Assert.Single(ids);
        Assert.Equal(issueId, ids[0]);
    }

    [Theory]
    [InlineData("closes #10")]
    [InlineData("fixes #10 — login regression")]
    [InlineData("resolves #10")]
    [InlineData("fix: #10 something")]
    public void ResolveCommitIssueIds_GitHubReference_ReturnsCorrectIssue(string message)
    {
        var issueId = Guid.NewGuid();
        var byGitHub = new Dictionary<int, Guid> { [10] = issueId };

        var result = BranchDetectionService.ResolveCommitIssueIds(message, NumberMap(), byGitHub);

        var ids = result.ToList();
        Assert.Single(ids);
        Assert.Equal(issueId, ids[0]);
    }

    [Fact]
    public void ResolveCommitIssueIds_NoReference_ReturnsEmpty()
    {
        var result = BranchDetectionService.ResolveCommitIssueIds("chore: update deps", NumberMap(1, 2), GitHubMap(3, 4));
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveCommitIssueIds_UnknownReference_ReturnsEmpty()
    {
        var result = BranchDetectionService.ResolveCommitIssueIds("fix: IP-999 not in project", NumberMap(1, 2), GitHubMap(3, 4));
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveCommitIssueIds_MultipleReferences_ReturnsAll()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var byNumber = new Dictionary<int, Guid> { [1] = id1, [2] = id2 };

        var result = BranchDetectionService.ResolveCommitIssueIds("IP-1 and IP-2 both fixed", byNumber, []).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(id1, result);
        Assert.Contains(id2, result);
    }

    [Fact]
    public void ResolveCommitIssueIds_DuplicateReferences_ReturnsDistinct()
    {
        var issueId = Guid.NewGuid();
        var byNumber = new Dictionary<int, Guid> { [5] = issueId };

        // IP-5 appears twice in message
        var result = BranchDetectionService.ResolveCommitIssueIds("IP-5 fixed, see ip-5", byNumber, []).ToList();

        Assert.Single(result);
        Assert.Equal(issueId, result[0]);
    }
}
