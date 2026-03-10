using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IssuePit.Api.Controllers;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

/// <summary>
/// Integration tests for the config-repo sync feature.
/// Each test writes JSON config files to a temp directory, sets the config-repo URL
/// via the API, triggers a sync, and verifies the resulting DB state.
/// </summary>
[Trait("Category", "Integration")]
public class ConfigRepoSyncTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task<(Guid tenantId, Guid orgId, Guid projectId, Guid userId)> SeedAsync(
        string orgSlug, string projectSlug, string username)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "CfgTest", Hostname = $"cfg-{tenantId}.test" });
        db.Organizations.Add(new Organization { Id = orgId, TenantId = tenantId, Name = "Cfg Org", Slug = orgSlug });
        db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Cfg Project", Slug = projectSlug });
        db.Users.Add(new User { Id = userId, TenantId = tenantId, Username = username, Email = $"{username}@test.com" });
        await db.SaveChangesAsync();

        return (tenantId, orgId, projectId, userId);
    }

    private static string CreateConfigDir() =>
        Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), $"issuepit-cfg-test-{Guid.NewGuid():N}")).FullName;

    private static void WriteModel<T>(string dir, string subDir, string fileName, T model)
    {
        var fullDir = Path.Combine(dir, subDir);
        Directory.CreateDirectory(fullDir);
        File.WriteAllText(Path.Combine(fullDir, fileName), JsonSerializer.Serialize(model, JsonOptions));
    }

    private async Task<HttpResponseMessage> SetConfigRepoAsync(Guid tenantId, string localPath, bool strict = false)
        => await _client.PutAsJsonAsync($"/api/admin/tenants/{tenantId}/config-repo",
               new ConfigRepoRequest(localPath, null, null, strict));

    private async Task<HttpResponseMessage> TriggerSyncAsync(Guid tenantId)
        => await _client.PostAsync($"/api/admin/tenants/{tenantId}/config-repo/sync", null);

    // -----------------------------------------------------------------------
    // Org config tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sync_OrgConfig_UpdatesNameAndCiCdSettings()
    {
        var orgSlug = $"sync-org-{Guid.NewGuid():N}"[..24];
        var (tenantId, orgId, _, _) = await SeedAsync(orgSlug, $"p-{Guid.NewGuid():N}"[..16], "u1");

        var dir = CreateConfigDir();
        WriteModel(dir, "orgs", $"{orgSlug}.json", new OrgConfigModel
        {
            Name = "Updated Org Name",
            MaxConcurrentRunners = 5,
            ActRunnerImage = "ubuntu:22.04"
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            var resp = await TriggerSyncAsync(tenantId);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var org = await db.Organizations.FindAsync(orgId);
            Assert.NotNull(org);
            Assert.Equal("Updated Org Name", org.Name);
            Assert.Equal(5, org.MaxConcurrentRunners);
            Assert.Equal("ubuntu:22.04", org.ActRunnerImage);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_OrgConfig_SlugFromFileName_WhenNoSlugInJson()
    {
        var orgSlug = $"fn-org-{Guid.NewGuid():N}"[..20];
        var (tenantId, orgId, _, _) = await SeedAsync(orgSlug, $"p-{Guid.NewGuid():N}"[..16], "u2");

        var dir = CreateConfigDir();
        WriteModel(dir, "orgs", $"{orgSlug}.json", new OrgConfigModel { Name = "From FileName" });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            await TriggerSyncAsync(tenantId);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var org = await db.Organizations.FindAsync(orgId);
            Assert.NotNull(org);
            Assert.Equal("From FileName", org.Name);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_OrgConfig_UnknownSlug_IsSkipped()
    {
        var orgSlug = $"real-org-{Guid.NewGuid():N}"[..20];
        var (tenantId, orgId, _, _) = await SeedAsync(orgSlug, $"p-{Guid.NewGuid():N}"[..16], "u3");

        var dir = CreateConfigDir();
        WriteModel(dir, "orgs", "nonexistent-org.json", new OrgConfigModel { Name = "Should Not Apply" });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            var resp = await TriggerSyncAsync(tenantId);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var org = await db.Organizations.FindAsync(orgId);
            Assert.NotNull(org);
            Assert.Equal("Cfg Org", org.Name); // unchanged
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_OrgConfig_NullFieldsInJson_DoNotOverwriteExistingValues()
    {
        var orgSlug = $"null-org-{Guid.NewGuid():N}"[..20];
        var (tenantId, orgId, _, _) = await SeedAsync(orgSlug, $"p-{Guid.NewGuid():N}"[..16], "u4");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var org = await db.Organizations.FindAsync(orgId);
            org!.ActRunnerImage = "custom:image";
            await db.SaveChangesAsync();
        }

        var dir = CreateConfigDir();
        // Only update name; actRunnerImage is not set in config file
        WriteModel(dir, "orgs", $"{orgSlug}.json", new OrgConfigModel { Name = "Partial Update" });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            await TriggerSyncAsync(tenantId);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var org = await db.Organizations.FindAsync(orgId);
            Assert.NotNull(org);
            Assert.Equal("Partial Update", org.Name);
            Assert.Equal("custom:image", org.ActRunnerImage); // unchanged — null field in config does not overwrite
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // -----------------------------------------------------------------------
    // Project config tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sync_ProjectConfig_UpdatesSettingsAndCreatesGitRepo()
    {
        var orgSlug = $"proj-org-{Guid.NewGuid():N}"[..20];
        var projSlug = $"proj-{Guid.NewGuid():N}"[..20];
        var (tenantId, _, projectId, _) = await SeedAsync(orgSlug, projSlug, "u5");

        var dir = CreateConfigDir();
        WriteModel(dir, "projects", $"{projSlug}.json", new ProjectConfigModel
        {
            OrgSlug = orgSlug,
            Description = "Config description",
            MaxConcurrentRunners = 2,
            GitUrl = "https://github.com/example/repo.git",
            GitToken = "ghp_testtoken",
            DefaultBranch = "develop"
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            await TriggerSyncAsync(tenantId);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

            var project = await db.Projects.FindAsync(projectId);
            Assert.NotNull(project);
            Assert.Equal("Config description", project.Description);
            Assert.Equal(2, project.MaxConcurrentRunners);

            var gitRepo = db.GitRepositories.FirstOrDefault(r => r.ProjectId == projectId);
            Assert.NotNull(gitRepo);
            Assert.Equal("https://github.com/example/repo.git", gitRepo.RemoteUrl);
            Assert.Equal("develop", gitRepo.DefaultBranch);
            Assert.Equal("ghp_testtoken", gitRepo.AuthToken);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_ProjectConfig_UpdatesExistingGitRepo()
    {
        var orgSlug = $"gitupd-org-{Guid.NewGuid():N}"[..20];
        var projSlug = $"gitupd-{Guid.NewGuid():N}"[..20];
        var (tenantId, _, projectId, _) = await SeedAsync(orgSlug, projSlug, "u6");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            db.GitRepositories.Add(new GitRepository
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                RemoteUrl = "https://github.com/old/repo.git",
                DefaultBranch = "main"
            });
            await db.SaveChangesAsync();
        }

        var dir = CreateConfigDir();
        WriteModel(dir, "projects", $"{projSlug}.json", new ProjectConfigModel
        {
            OrgSlug = orgSlug,
            GitUrl = "https://github.com/new/repo.git",
            GitToken = "new-token",
            DefaultBranch = "main"
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            await TriggerSyncAsync(tenantId);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var gitRepo = db.GitRepositories.FirstOrDefault(r => r.ProjectId == projectId);
            Assert.NotNull(gitRepo);
            Assert.Equal("https://github.com/new/repo.git", gitRepo.RemoteUrl);
            Assert.Equal("new-token", gitRepo.AuthToken);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // -----------------------------------------------------------------------
    // Member resolution tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sync_OrgMembers_ByUsername_NonStrictMode_AddsWithRole()
    {
        var orgSlug = $"mem-org-{Guid.NewGuid():N}"[..20];
        var username = $"memuser-{Guid.NewGuid():N}"[..20];
        var (tenantId, orgId, _, userId) = await SeedAsync(orgSlug, $"p-{Guid.NewGuid():N}"[..16], username);

        var dir = CreateConfigDir();
        WriteModel(dir, "orgs", $"{orgSlug}.json", new OrgConfigModel
        {
            Members = [new OrgMemberConfigModel { Username = username, Role = OrgRole.Admin }]
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir, strict: false);
            await TriggerSyncAsync(tenantId);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var member = db.OrganizationMembers.FirstOrDefault(m => m.OrgId == orgId && m.UserId == userId);
            Assert.NotNull(member);
            Assert.Equal(OrgRole.Admin, member.Role);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_OrgMembers_ByUserId_AddsWithRole()
    {
        var orgSlug = $"uid-org-{Guid.NewGuid():N}"[..20];
        var (tenantId, orgId, _, userId) = await SeedAsync(orgSlug, $"p-{Guid.NewGuid():N}"[..16], "uiduser");

        var dir = CreateConfigDir();
        WriteModel(dir, "orgs", $"{orgSlug}.json", new OrgConfigModel
        {
            Members = [new OrgMemberConfigModel { UserId = userId, Role = OrgRole.Owner }]
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            await TriggerSyncAsync(tenantId);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var member = db.OrganizationMembers.FirstOrDefault(m => m.OrgId == orgId && m.UserId == userId);
            Assert.NotNull(member);
            Assert.Equal(OrgRole.Owner, member.Role);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_OrgMembers_UnknownUsername_NonStrictMode_IsSkipped()
    {
        var orgSlug = $"skip-org-{Guid.NewGuid():N}"[..20];
        var (tenantId, orgId, _, _) = await SeedAsync(orgSlug, $"p-{Guid.NewGuid():N}"[..16], "realuser");

        var dir = CreateConfigDir();
        WriteModel(dir, "orgs", $"{orgSlug}.json", new OrgConfigModel
        {
            Members = [new OrgMemberConfigModel { Username = "ghost_user_not_in_db", Role = OrgRole.Admin }]
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir, strict: false);
            var resp = await TriggerSyncAsync(tenantId);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            Assert.Equal(0, db.OrganizationMembers.Count(m => m.OrgId == orgId));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_ProjectMembers_ByUsername_AddsWithPermissions()
    {
        var orgSlug = $"pmem-org-{Guid.NewGuid():N}"[..20];
        var projSlug = $"pmem-{Guid.NewGuid():N}"[..20];
        var username = $"pmemuser-{Guid.NewGuid():N}"[..20];
        var (tenantId, _, projectId, userId) = await SeedAsync(orgSlug, projSlug, username);

        var dir = CreateConfigDir();
        WriteModel(dir, "projects", $"{projSlug}.json", new ProjectConfigModel
        {
            OrgSlug = orgSlug,
            Members = [new ProjectMemberConfigModel { Username = username, Permissions = ProjectPermission.Read | ProjectPermission.Write }]
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            await TriggerSyncAsync(tenantId);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var member = db.ProjectMembers.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId);
            Assert.NotNull(member);
            Assert.Equal(ProjectPermission.Read | ProjectPermission.Write, member.Permissions);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_ExistingMember_UpdatesRole_NotDuplicated()
    {
        var orgSlug = $"upd-org-{Guid.NewGuid():N}"[..20];
        var username = $"upduser-{Guid.NewGuid():N}"[..20];
        var (tenantId, orgId, _, userId) = await SeedAsync(orgSlug, $"p-{Guid.NewGuid():N}"[..16], username);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            db.OrganizationMembers.Add(new OrganizationMember { OrgId = orgId, UserId = userId, Role = OrgRole.Member });
            await db.SaveChangesAsync();
        }

        var dir = CreateConfigDir();
        WriteModel(dir, "orgs", $"{orgSlug}.json", new OrgConfigModel
        {
            Members = [new OrgMemberConfigModel { Username = username, Role = OrgRole.Admin }]
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            await TriggerSyncAsync(tenantId);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var members = db.OrganizationMembers.Where(m => m.OrgId == orgId && m.UserId == userId).ToList();
            Assert.Single(members); // no duplicate
            Assert.Equal(OrgRole.Admin, members[0].Role);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // -----------------------------------------------------------------------
    // API validation tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sync_NoConfigRepoConfigured_ReturnsBadRequest()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "NoCfg", Hostname = $"nocfg-{Guid.NewGuid()}.test" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var resp = await TriggerSyncAsync(tenant.Id);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Sync_NonExistentTenant_ReturnsNotFound()
    {
        var resp = await TriggerSyncAsync(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
