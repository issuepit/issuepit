using IssuePit.McpServer;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class McpServerOptionsTests
{
    [Fact]
    public void McpServerOptions_NonDestructive_DefaultsToTrue()
    {
        var opts = new McpServerOptions();
        Assert.True(opts.NonDestructive);
    }

    [Fact]
    public void McpServerOptions_AgentMode_DefaultsToFalse()
    {
        var opts = new McpServerOptions();
        Assert.False(opts.AgentMode);
    }

    [Fact]
    public void McpServerOptions_ProjectId_DefaultsToNull()
    {
        var opts = new McpServerOptions();
        Assert.Null(opts.ProjectId);
    }

    [Fact]
    public void McpServerOptions_Section_IsIssuePit()
    {
        Assert.Equal("IssuePit", McpServerOptions.Section);
    }
}
