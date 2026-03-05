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
}
