using IssuePit.Api.Services;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class GitServiceUsernameResolutionTests
{
    // Fine-grained PATs require the literal "x-access-token" username on the git smart-HTTP
    // endpoint — using any other username (including the GitHub login) returns HTTP 403.
    // This is the most common reason that a token works against the REST API (which uses
    // Bearer auth and ignores the username) yet fails the git fetch with 403.
    [Theory]
    [InlineData("https://github.com/owner/repo.git", "bend-work", "github_pat_ABC123")]
    [InlineData("https://GitHub.com/owner/repo.git", "alice", "github_pat_xyz")]
    [InlineData("https://github.com/owner/repo.git", null, "github_pat_xyz")]
    public void FineGrainedPat_OnGitHub_OverridesUsernameToXAccessToken(string url, string? user, string token)
    {
        Assert.Equal("x-access-token", GitService.ResolveGitUsername(url, user, token));
    }

    // GitHub App installation tokens (ghs_...) likewise require "x-access-token".
    [Fact]
    public void AppInstallationToken_OnGitHub_OverridesUsernameToXAccessToken()
    {
        Assert.Equal("x-access-token",
            GitService.ResolveGitUsername("https://github.com/o/r.git", "myuser", "ghs_AppInstallToken"));
    }

    // Classic PATs accept any username, so the configured one is preserved.
    [Theory]
    [InlineData("https://github.com/owner/repo.git", "bend-work", "ghp_ClassicToken", "bend-work")]
    [InlineData("https://github.com/owner/repo.git", null, "ghp_ClassicToken", "git")]
    [InlineData("https://github.com/owner/repo.git", "", "ghp_ClassicToken", "git")]
    public void ClassicPat_PreservesConfiguredUsername(string url, string? user, string token, string expected)
    {
        Assert.Equal(expected, GitService.ResolveGitUsername(url, user, token));
    }

    // The override is github.com-specific — other hosts (GitLab, Bitbucket, self-hosted) are not touched.
    [Fact]
    public void NonGitHubHost_DoesNotOverrideUsername_EvenForGitHubLikeTokenPrefix()
    {
        Assert.Equal("alice",
            GitService.ResolveGitUsername("https://gitlab.example.com/o/r.git", "alice", "github_pat_xyz"));
    }

    // No token → username defaults to "git" so libgit2 still has something to send for SSH-style URLs.
    [Fact]
    public void EmptyToken_ReturnsConfiguredUsernameOrGitDefault()
    {
        Assert.Equal("alice", GitService.ResolveGitUsername("https://github.com/o/r.git", "alice", null));
        Assert.Equal("git", GitService.ResolveGitUsername("https://github.com/o/r.git", null, null));
    }
}
