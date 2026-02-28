using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class CiCdRunEntityTests
{
    [Fact]
    public void CiCdRun_DefaultStatus_IsPending()
    {
        var run = new CiCdRun();
        Assert.Equal(CiCdRunStatus.Pending, run.Status);
    }

    [Fact]
    public void CiCdRun_Logs_InitialisedEmpty()
    {
        var run = new CiCdRun();
        Assert.Empty(run.Logs);
    }

    [Fact]
    public void CiCdRun_AgentSessionId_NullableByDefault()
    {
        var run = new CiCdRun();
        Assert.Null(run.AgentSessionId);
    }

    [Fact]
    public void CiCdRunLog_DefaultStream_IsStdout()
    {
        var log = new CiCdRunLog();
        Assert.Equal(LogStream.Stdout, log.Stream);
    }

    [Fact]
    public void CiCdRun_ExternalFields_NullableByDefault()
    {
        var run = new CiCdRun();
        Assert.Null(run.ExternalSource);
        Assert.Null(run.ExternalRunId);
    }
}
