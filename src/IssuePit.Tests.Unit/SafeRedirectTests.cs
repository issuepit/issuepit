using IssuePit.Api.Services;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class SafeRedirectTests
{
    [Theory]
    [InlineData("/projects/123/settings", "/projects/123/settings")]
    [InlineData("/config/github-identities", "/config/github-identities")]
    [InlineData("/with?query=1&x=y", "/with?query=1&x=y")]
    public void SafePaths_AreReturnedUnchanged(string input, string expected)
    {
        Assert.Equal(expected, SafeRedirect.SanitisePath(input, "/fallback"));
    }

    // Open-redirect protection: protocol-relative paths must NOT be accepted because
    // concatenating them with a frontend base URL can navigate to attacker-controlled hosts
    // (e.g. "https://app.example.com" + "//evil.com" → browser navigates to https://evil.com).
    [Theory]
    [InlineData("//evil.com")]
    [InlineData("//evil.com/path")]
    [InlineData("/\\evil.com")]
    [InlineData("/\\\\evil.com")]
    public void ProtocolRelativePaths_AreRejected(string input)
    {
        Assert.Equal("/fallback", SafeRedirect.SanitisePath(input, "/fallback"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.com")]    // absolute URL
    [InlineData("javascript:alert(1)")]  // pseudo-URL
    [InlineData("relative/path")]        // missing leading slash
    [InlineData("../../etc/passwd")]     // traversal-style
    public void NonSamePaths_FallBackToDefault(string? input)
    {
        Assert.Equal("/fallback", SafeRedirect.SanitisePath(input, "/fallback"));
    }
}
