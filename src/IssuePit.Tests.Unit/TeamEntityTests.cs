using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.Tests.Unit;

public class TeamEntityTests
{
    [Fact]
    public void ProjectPermission_IsFlags_CanCombine()
    {
        var permissions = ProjectPermission.Read | ProjectPermission.Write | ProjectPermission.MoveKanban;
        Assert.True(permissions.HasFlag(ProjectPermission.Read));
        Assert.True(permissions.HasFlag(ProjectPermission.Write));
        Assert.True(permissions.HasFlag(ProjectPermission.MoveKanban));
        Assert.False(permissions.HasFlag(ProjectPermission.ProjectAdmin));
    }
}
