using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.Tests.Unit;

public class AgentSessionEntityTests
{
    [Fact]
    public void AgentSession_DefaultStatus_IsPending()
    {
        var session = new AgentSession();
        Assert.Equal(AgentSessionStatus.Pending, session.Status);
    }

    [Fact]
    public void AgentSession_StartedAt_SetOnConstruction()
    {
        var before = DateTime.UtcNow;
        var session = new AgentSession();
        Assert.True(session.StartedAt >= before);
    }

    [Fact]
    public void AgentSession_EndedAt_NullByDefault()
    {
        var session = new AgentSession();
        Assert.Null(session.EndedAt);
    }
}
