using System.Text.RegularExpressions;
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

    /// <summary>Builds the slug regex for branch name segments (same pattern as the service uses internally).</summary>
    private static Regex BranchSlug(string issueKey) =>
        new($@"{Regex.Escape(issueKey)}-?(\d+)", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    /// <summary>Builds the slug regex for commit messages (same pattern as the service uses internally).</summary>
    private static Regex CommitSlug(string issueKey) =>
        new($@"\b{Regex.Escape(issueKey)}-?(\d+)\b", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

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
    [InlineData("feat/ip-123-another", 123)]
    [InlineData("feat/ip123-another-branch", 123)]
    [InlineData("fix/IP-456-some-fix", 456)]
    [InlineData("chore/IP456", 456)]
    public void ResolveBranchIssueIds_ProjectSlugPrefix_ReturnsCorrectIssue(string branchName, int expectedNumber)
    {
        var issueId = Guid.NewGuid();
        var byNumber = new Dictionary<int, Guid> { [expectedNumber] = issueId };

        var result = BranchDetectionService.ResolveBranchIssueIds(branchName, byNumber, [], BranchSlug("IP"));

        var ids = result.ToList();
        Assert.Single(ids);
        Assert.Equal(issueId, ids[0]);
    }

    [Fact]
    public void ResolveBranchIssueIds_SlugPrefix_OnlyMatchesProjectSlug()
    {
        // A branch using prefix "IP" should NOT match when a different project slug "PROJ" is active
        var issueId = Guid.NewGuid();
        var byNumber = new Dictionary<int, Guid> { [123] = issueId };

        var result = BranchDetectionService.ResolveBranchIssueIds("feat/ip-123-thing", byNumber, [], BranchSlug("PROJ"));

        // ip-123 is not a plain number and doesn't match "PROJ" → no match
        Assert.Empty(result);
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
    public void ResolveCommitIssueIds_ProjectSlugKey_ReturnsCorrectIssue(string message)
    {
        var issueId = Guid.NewGuid();
        var byNumber = new Dictionary<int, Guid> { [42] = issueId };

        var result = BranchDetectionService.ResolveCommitIssueIds(message, byNumber, [], CommitSlug("IP"));

        var ids = result.ToList();
        Assert.Single(ids);
        Assert.Equal(issueId, ids[0]);
    }

    [Fact]
    public void ResolveCommitIssueIds_WrongSlug_ReturnsEmpty()
    {
        // "IP-42" should not match when the project's IssueKey is "PROJ"
        var issueId = Guid.NewGuid();
        var byNumber = new Dictionary<int, Guid> { [42] = issueId };

        var result = BranchDetectionService.ResolveCommitIssueIds("fix: IP-42 resolved", byNumber, [], CommitSlug("PROJ"));

        Assert.Empty(result);
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
        var result = BranchDetectionService.ResolveCommitIssueIds("fix: IP-999 not in project", NumberMap(1, 2), GitHubMap(3, 4), CommitSlug("IP"));
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveCommitIssueIds_MultipleReferences_ReturnsAll()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var byNumber = new Dictionary<int, Guid> { [1] = id1, [2] = id2 };

        var result = BranchDetectionService.ResolveCommitIssueIds("IP-1 and IP-2 both fixed", byNumber, [], CommitSlug("IP")).ToList();

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
        var result = BranchDetectionService.ResolveCommitIssueIds("IP-5 fixed, see ip-5", byNumber, [], CommitSlug("IP")).ToList();

        Assert.Single(result);
        Assert.Equal(issueId, result[0]);
    }

    [Fact]
    public void ResolveCommitIssueIds_NoSlug_OnlyGitHubRefsMatch()
    {
        var ipIssueId = Guid.NewGuid();
        var ghIssueId = Guid.NewGuid();
        var byNumber = new Dictionary<int, Guid> { [42] = ipIssueId };
        var byGitHub = new Dictionary<int, Guid> { [10] = ghIssueId };

        // Without a slug regex, "IP-42" should NOT match, but "#10" should
        var result = BranchDetectionService.ResolveCommitIssueIds("IP-42 and closes #10", byNumber, byGitHub).ToList();

        Assert.Single(result);
        Assert.Equal(ghIssueId, result[0]);
    }
}
