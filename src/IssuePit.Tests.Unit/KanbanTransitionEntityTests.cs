using IssuePit.Core.Entities;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class KanbanTransitionEntityTests
{
    [Fact]
    public void KanbanTransition_DefaultIsAuto_IsFalse()
    {
        var transition = new KanbanTransition();
        Assert.False(transition.IsAuto);
    }

    [Fact]
    public void KanbanTransition_DefaultAgentId_IsNull()
    {
        var transition = new KanbanTransition();
        Assert.Null(transition.AgentId);
    }

    [Fact]
    public void KanbanTransition_CanSetAutoWithAgent()
    {
        var agentId = Guid.NewGuid();
        var transition = new KanbanTransition { IsAuto = true, AgentId = agentId };
        Assert.True(transition.IsAuto);
        Assert.Equal(agentId, transition.AgentId);
    }

    [Fact]
    public void KanbanTransition_CreatedAt_DefaultsToUtcNow()
    {
        var before = DateTime.UtcNow;
        var transition = new KanbanTransition();
        Assert.True(transition.CreatedAt >= before);
    }
}
