using IssuePit.CiCdClient.Workers;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class CiCdWorkerEnvVarsTests
{
    private static CiCdRun BuildRun(Guid? projectId = null, Guid? runId = null) =>
        new()
        {
            Id = runId ?? Guid.NewGuid(),
            ProjectId = projectId ?? Guid.NewGuid(),
            CommitSha = "abc123",
            Status = CiCdRunStatus.Running,
            StartedAt = DateTime.UtcNow,
        };

    private static IEnumerable<string> GetEnvValues(string envBlock)
    {
        var list = new List<string>();
        foreach (var line in envBlock.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
                list.Add(trimmed);
        }
        return list;
    }

    [Fact]
    public void PrependIssuePitEnvVars_AlwaysContainsIssuePitRunTrue()
    {
        var run = BuildRun();
        var result = CiCdWorker.PrependIssuePitEnvVars(null, run, orgId: null);
        Assert.Contains("ISSUEPIT_RUN=true", GetEnvValues(result));
    }

    [Fact]
    public void PrependIssuePitEnvVars_AlwaysContainsProjectId()
    {
        var projectId = Guid.NewGuid();
        var run = BuildRun(projectId: projectId);
        var result = CiCdWorker.PrependIssuePitEnvVars(null, run, orgId: null);
        Assert.Contains($"ISSUEPIT_PROJECT_ID={projectId}", GetEnvValues(result));
    }

    [Fact]
    public void PrependIssuePitEnvVars_AlwaysContainsRunId()
    {
        var runId = Guid.NewGuid();
        var run = BuildRun(runId: runId);
        var result = CiCdWorker.PrependIssuePitEnvVars(null, run, orgId: null);
        Assert.Contains($"ISSUEPIT_RUN_ID={runId}", GetEnvValues(result));
    }

    [Fact]
    public void PrependIssuePitEnvVars_WithOrgId_ContainsOrgId()
    {
        var orgId = Guid.NewGuid();
        var run = BuildRun();
        var result = CiCdWorker.PrependIssuePitEnvVars(null, run, orgId: orgId);
        Assert.Contains($"ISSUEPIT_ORG_ID={orgId}", GetEnvValues(result));
    }

    [Fact]
    public void PrependIssuePitEnvVars_WithoutOrgId_NoOrgIdVar()
    {
        var run = BuildRun();
        var result = CiCdWorker.PrependIssuePitEnvVars(null, run, orgId: null);
        Assert.DoesNotContain(GetEnvValues(result), v => v.StartsWith("ISSUEPIT_ORG_ID=", StringComparison.Ordinal));
    }

    [Fact]
    public void PrependIssuePitEnvVars_NullExistingEnv_ReturnsOnlyIssuePitVars()
    {
        var run = BuildRun();
        var result = CiCdWorker.PrependIssuePitEnvVars(null, run, orgId: null);
        var lines = GetEnvValues(result).ToList();
        Assert.All(lines, l => Assert.StartsWith("ISSUEPIT_", l));
    }

    [Fact]
    public void PrependIssuePitEnvVars_ExistingEnv_IsAppendedAfterIssuePitVars()
    {
        var run = BuildRun();
        var existing = "MY_VAR=hello\nOTHER_VAR=world";
        var result = CiCdWorker.PrependIssuePitEnvVars(existing, run, orgId: null);
        var lines = GetEnvValues(result).ToList();

        // ISSUEPIT_RUN=true should appear before MY_VAR
        var issuepitIdx = lines.IndexOf("ISSUEPIT_RUN=true");
        var myVarIdx = lines.IndexOf("MY_VAR=hello");
        Assert.True(issuepitIdx >= 0, "ISSUEPIT_RUN=true not found");
        Assert.True(myVarIdx >= 0, "MY_VAR=hello not found");
        Assert.True(issuepitIdx < myVarIdx, "ISSUEPIT_RUN=true should come before user vars");
    }

    [Fact]
    public void PrependIssuePitEnvVars_ExistingEnv_UserVarsPreserved()
    {
        var run = BuildRun();
        var existing = "MY_VAR=hello\nOTHER_VAR=world";
        var result = CiCdWorker.PrependIssuePitEnvVars(existing, run, orgId: null);
        var lines = GetEnvValues(result).ToList();
        Assert.Contains("MY_VAR=hello", lines);
        Assert.Contains("OTHER_VAR=world", lines);
    }

    [Fact]
    public void PrependIssuePitEnvVars_EmptyExistingEnv_ReturnsOnlyIssuePitVars()
    {
        var run = BuildRun();
        var result = CiCdWorker.PrependIssuePitEnvVars("", run, orgId: null);
        var lines = GetEnvValues(result).ToList();
        Assert.All(lines, l => Assert.StartsWith("ISSUEPIT_", l));
    }

    [Fact]
    public void PrependIssuePitEnvVars_WhitespaceExistingEnv_TreatedAsEmpty()
    {
        var run = BuildRun();
        var result = CiCdWorker.PrependIssuePitEnvVars("   ", run, orgId: null);
        var lines = GetEnvValues(result).ToList();
        Assert.All(lines, l => Assert.StartsWith("ISSUEPIT_", l));
    }

    // ── IsValidFullSha ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("a3f1b2c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0")] // lowercase
    [InlineData("A3F1B2C4D5E6F7A8B9C0D1E2F3A4B5C6D7E8F9A0")] // uppercase
    [InlineData("A3f1B2c4D5e6F7a8B9c0D1e2F3a4B5c6D7e8F9a0")] // mixed case
    public void IsValidFullSha_Valid40HexChars_ReturnsTrue(string sha)
    {
        Assert.True(CiCdWorker.IsValidFullSha(sha));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("main")]
    [InlineData("feature/my-branch")]
    [InlineData("abc123")]                                       // short SHA (6 chars)
    [InlineData("a3f1b2c")]                                      // short SHA (7 chars)
    [InlineData("a3f1b2c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0x")] // 41 chars
    [InlineData("a3f1b2c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9g0")]  // invalid hex char 'g'
    public void IsValidFullSha_Invalid_ReturnsFalse(string? sha)
    {
        Assert.False(CiCdWorker.IsValidFullSha(sha));
    }
}
