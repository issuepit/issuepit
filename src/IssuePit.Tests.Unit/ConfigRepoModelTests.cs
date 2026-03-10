using IssuePit.Api.Services;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class ConfigRepoModelTests
{
    [Fact]
    public void OrgMemberConfigModel_DefaultRole_IsMember()
    {
        var model = new OrgMemberConfigModel();
        Assert.Equal(OrgRole.Member, model.Role);
    }

    [Fact]
    public void ProjectMemberConfigModel_DefaultPermissions_IsRead()
    {
        var model = new ProjectMemberConfigModel();
        Assert.Equal(ProjectPermission.Read, model.Permissions);
    }

    [Fact]
    public void Tenant_DefaultConfigStrictMode_IsFalse()
    {
        var tenant = new Tenant();
        Assert.False(tenant.ConfigStrictMode);
        Assert.Null(tenant.ConfigRepoUrl);
        Assert.Null(tenant.ConfigRepoToken);
        Assert.Null(tenant.ConfigRepoUsername);
    }

    [Fact]
    public void OrgConfigModel_AllPropertiesNullByDefault()
    {
        var model = new OrgConfigModel();
        Assert.Null(model.Name);
        Assert.Null(model.Slug);
        Assert.Null(model.Members);
        Assert.Null(model.MaxConcurrentRunners);
        Assert.Null(model.ActRunnerImage);
    }

    [Fact]
    public void ProjectConfigModel_AllPropertiesNullByDefault()
    {
        var model = new ProjectConfigModel();
        Assert.Null(model.Name);
        Assert.Null(model.Slug);
        Assert.Null(model.OrgSlug);
        Assert.Null(model.GitUrl);
        Assert.Null(model.GitToken);
        Assert.Null(model.Members);
    }
}
