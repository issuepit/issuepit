using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IssuePit.Api.Controllers;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;
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
        WriteModel(dir, "orgs", $"{orgSlug}.json5", new OrgConfigModel
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
        WriteModel(dir, "orgs", $"{orgSlug}.json5", new OrgConfigModel { Name = "From FileName" });

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
    public async Task Sync_OrgConfig_UnknownSlug_IsCreated()
    {
        var existingOrgSlug = $"real-org-{Guid.NewGuid():N}"[..20];
        var (tenantId, orgId, _, _) = await SeedAsync(existingOrgSlug, $"p-{Guid.NewGuid():N}"[..16], "u3");
        var newOrgSlug = $"neworg-{Guid.NewGuid():N}"[..20];

        var dir = CreateConfigDir();
        WriteModel(dir, "orgs", $"{newOrgSlug}.json5", new OrgConfigModel { Name = "Created Org" });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            var resp = await TriggerSyncAsync(tenantId);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

            // Existing org must remain unchanged
            var existingOrg = await db.Organizations.FindAsync(orgId);
            Assert.NotNull(existingOrg);
            Assert.Equal("Cfg Org", existingOrg.Name);

            // New org must have been created from the config file
            var newOrg = await db.Organizations
                .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Slug == newOrgSlug);
            Assert.NotNull(newOrg);
            Assert.Equal("Created Org", newOrg.Name);
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
        WriteModel(dir, "orgs", $"{orgSlug}.json5", new OrgConfigModel { Name = "Partial Update" });

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
        WriteModel(dir, "projects", $"{projSlug}.json5", new ProjectConfigModel
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
        WriteModel(dir, "projects", $"{projSlug}.json5", new ProjectConfigModel
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
        WriteModel(dir, "orgs", $"{orgSlug}.json5", new OrgConfigModel
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
        WriteModel(dir, "orgs", $"{orgSlug}.json5", new OrgConfigModel
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
        WriteModel(dir, "orgs", $"{orgSlug}.json5", new OrgConfigModel
        {
            Members = [new OrgMemberConfigModel { Username = "ghost_user_not_in_db", Role = OrgRole.Admin }]
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir, strict: false);
            var resp = await TriggerSyncAsync(tenantId);
            // Non-strict mode: unknown user is recorded as a warning, not an error → 200 OK.
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var issues = body.GetProperty("issues").EnumerateArray().ToList();
            Assert.Contains(issues, i =>
                i.GetProperty("severity").GetString() == "warning" &&
                i.GetProperty("message").GetString()!.Contains("ghost_user_not_in_db"));

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
        WriteModel(dir, "projects", $"{projSlug}.json5", new ProjectConfigModel
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
        WriteModel(dir, "orgs", $"{orgSlug}.json5", new OrgConfigModel
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

    // -----------------------------------------------------------------------
    // Multiple git origins (gitRepos array)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sync_ProjectConfig_MultipleGitOrigins_CreatesAllModes()
    {
        var orgSlug = $"multi-org-{Guid.NewGuid():N}"[..20];
        var projSlug = $"multi-{Guid.NewGuid():N}"[..20];
        var (tenantId, _, projectId, _) = await SeedAsync(orgSlug, projSlug, "u7");

        var dir = CreateConfigDir();
        WriteModel(dir, "projects", $"{projSlug}.json5", new ProjectConfigModel
        {
            OrgSlug = orgSlug,
            GitRepos =
            [
                new GitRepoConfigModel
                {
                    RemoteUrl = "https://github.com/example/my-project.git",
                    GitToken = "ghp_working_token",
                    DefaultBranch = "main",
                    Mode = GitOriginMode.Working
                },
                new GitRepoConfigModel
                {
                    RemoteUrl = "https://github.com/example/my-project-releases.git",
                    GitToken = "ghp_release_token",
                    DefaultBranch = "main",
                    Mode = GitOriginMode.Release
                }
            ]
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            await TriggerSyncAsync(tenantId);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var repos = db.GitRepositories.Where(r => r.ProjectId == projectId).ToList();
            Assert.Equal(2, repos.Count);

            var workingRepo = repos.Single(r => r.Mode == GitOriginMode.Working);
            Assert.Equal("https://github.com/example/my-project.git", workingRepo.RemoteUrl);
            Assert.Equal("ghp_working_token", workingRepo.AuthToken);

            var releaseRepo = repos.Single(r => r.Mode == GitOriginMode.Release);
            Assert.Equal("https://github.com/example/my-project-releases.git", releaseRepo.RemoteUrl);
            Assert.Equal("ghp_release_token", releaseRepo.AuthToken);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_ProjectConfig_GitRepos_UpsertsWithoutRemovingExisting()
    {
        var orgSlug = $"obs-org-{Guid.NewGuid():N}"[..20];
        var projSlug = $"obs-{Guid.NewGuid():N}"[..20];
        var (tenantId, _, projectId, _) = await SeedAsync(orgSlug, projSlug, "u8");

        // Pre-seed two repos; config only mentions one — both should remain (no deletion)
        using (var setup = factory.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            db.GitRepositories.AddRange(
                new GitRepository
                {
                    Id = Guid.NewGuid(), ProjectId = projectId,
                    RemoteUrl = "https://github.com/example/keep.git", DefaultBranch = "main", Mode = GitOriginMode.Working
                },
                new GitRepository
                {
                    Id = Guid.NewGuid(), ProjectId = projectId,
                    RemoteUrl = "https://github.com/example/another.git", DefaultBranch = "main", Mode = GitOriginMode.ReadOnly
                });
            await db.SaveChangesAsync();
        }

        var dir = CreateConfigDir();
        WriteModel(dir, "projects", $"{projSlug}.json5", new ProjectConfigModel
        {
            OrgSlug = orgSlug,
            GitRepos =
            [
                new GitRepoConfigModel
                {
                    RemoteUrl = "https://github.com/example/keep.git",
                    GitToken = "new-token",
                    Mode = GitOriginMode.Working
                }
            ]
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            await TriggerSyncAsync(tenantId);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var repos = db.GitRepositories.Where(r => r.ProjectId == projectId).ToList();
            // Both DB repos remain (no removal) — only the mentioned one is updated
            Assert.Equal(2, repos.Count);
            var updated = repos.Single(r => r.RemoteUrl == "https://github.com/example/keep.git");
            Assert.Equal("new-token", updated.AuthToken);
            // The non-mentioned one is untouched
            Assert.Contains(repos, r => r.RemoteUrl == "https://github.com/example/another.git");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_ProjectConfig_WithLocalRepositories_SetsReusableWorkflowPaths()
    {
        var orgSlug = $"wf-org-{Guid.NewGuid():N}"[..20];
        var projSlug = $"wf-{Guid.NewGuid():N}"[..20];
        var (tenantId, _, projectId, _) = await SeedAsync(orgSlug, projSlug, "u9");

        var dir = CreateConfigDir();
        WriteModel(dir, "projects", $"{projSlug}.json5", new ProjectConfigModel
        {
            OrgSlug = orgSlug,
            LocalRepositories = "my-org/reusable-workflows=/home/runner/local/reusable-workflows",
            ActionCachePath = "/home/runner/act-cache",
            UseNewActionCache = true
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            await TriggerSyncAsync(tenantId);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var project = await db.Projects.FindAsync(projectId);
            Assert.NotNull(project);
            Assert.Equal("my-org/reusable-workflows=/home/runner/local/reusable-workflows", project.LocalRepositories);
            Assert.Equal("/home/runner/act-cache", project.ActionCachePath);
            Assert.True(project.UseNewActionCache);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // -----------------------------------------------------------------------
    // Resilience / error handling
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sync_MalformedJsonFile_IsSkipped_OtherFilesStillApplied()
    {
        var orgSlug1 = $"ok-org-{Guid.NewGuid():N}"[..20];
        var orgSlug2 = $"bad-org-{Guid.NewGuid():N}"[..20];
        var (tenantId, orgId1, _, _) = await SeedAsync(orgSlug1, $"p-{Guid.NewGuid():N}"[..16], "u10");

        // Seed a second org so we have something to verify unchanged
        using (var setup = factory.Services.CreateScope())
        {
            var db2 = setup.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            db2.Organizations.Add(new Organization
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Bad Org", Slug = orgSlug2
            });
            await db2.SaveChangesAsync();
        }

        var dir = CreateConfigDir();
        // Write a valid config for org1
        WriteModel(dir, "orgs", $"{orgSlug1}.json5", new OrgConfigModel { Name = "Valid Updated" });
        // Write deliberately broken JSON for org2
        var orgsDir = Path.Combine(dir, "orgs");
        File.WriteAllText(Path.Combine(orgsDir, $"{orgSlug2}.json"), "{{ not valid json }");

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            var resp = await TriggerSyncAsync(tenantId);
            // Sync should still succeed overall
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            // Valid file was applied
            var org1 = await db.Organizations.FindAsync(orgId1);
            Assert.NotNull(org1);
            Assert.Equal("Valid Updated", org1.Name);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_ProjectConfig_UnknownOrgSlug_IsSkipped()
    {
        var orgSlug = $"punkorg-{Guid.NewGuid():N}"[..20];
        var projSlug = $"punk-{Guid.NewGuid():N}"[..20];
        var (tenantId, _, projectId, _) = await SeedAsync(orgSlug, projSlug, "u11");

        var dir = CreateConfigDir();
        WriteModel(dir, "projects", $"{projSlug}.json5", new ProjectConfigModel
        {
            OrgSlug = "nonexistent-org-slug", // org not in DB
            Name = "Should Not Apply"
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            var resp = await TriggerSyncAsync(tenantId);
            // Non-strict mode: unknown org slug is a warning, not an error → 200 OK.
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var issues = body.GetProperty("issues").EnumerateArray().ToList();
            Assert.Contains(issues, i =>
                i.GetProperty("severity").GetString() == "warning" &&
                i.GetProperty("message").GetString()!.Contains("nonexistent-org-slug"));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var project = await db.Projects.FindAsync(projectId);
            Assert.NotNull(project);
            Assert.Equal("Cfg Project", project.Name); // unchanged
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_ProjectConfig_UnknownSlug_WithOrgSlug_IsCreated()
    {
        var orgSlug = $"newproj-org-{Guid.NewGuid():N}"[..20];
        var existingProjSlug = $"existing-{Guid.NewGuid():N}"[..20];
        var (tenantId, orgId, _, _) = await SeedAsync(orgSlug, existingProjSlug, "u14");
        var newProjSlug = $"newproj-{Guid.NewGuid():N}"[..20];

        var dir = CreateConfigDir();
        WriteModel(dir, "projects", $"{newProjSlug}.json5", new ProjectConfigModel
        {
            OrgSlug = orgSlug,
            Name = "Auto Created Project",
            Description = "Created via config repo"
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            var resp = await TriggerSyncAsync(tenantId);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var newProject = await db.Projects
                .FirstOrDefaultAsync(p => p.OrgId == orgId && p.Slug == newProjSlug);
            Assert.NotNull(newProject);
            Assert.Equal("Auto Created Project", newProject.Name);
            Assert.Equal("Created via config repo", newProject.Description);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_ProjectConfig_UnknownSlug_WithOrgSlug_NoName_UsesSlugAsName()
    {
        var orgSlug = $"noname-org-{Guid.NewGuid():N}"[..20];
        var existingProjSlug = $"existing-{Guid.NewGuid():N}"[..20];
        var (tenantId, orgId, _, _) = await SeedAsync(orgSlug, existingProjSlug, "u16");
        var newProjSlug = $"noname-proj-{Guid.NewGuid():N}"[..20];

        var dir = CreateConfigDir();
        WriteModel(dir, "projects", $"{newProjSlug}.json5", new ProjectConfigModel
        {
            OrgSlug = orgSlug,
            // Name intentionally omitted — slug should be used as fallback
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            var resp = await TriggerSyncAsync(tenantId);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var newProject = await db.Projects
                .FirstOrDefaultAsync(p => p.OrgId == orgId && p.Slug == newProjSlug);
            Assert.NotNull(newProject);
            Assert.Equal(newProjSlug, newProject.Name); // slug used as fallback name
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_ProjectConfig_UnknownSlug_WithoutOrgSlug_IsSkippedWithWarning()    {
        var orgSlug = $"noslug-org-{Guid.NewGuid():N}"[..20];
        var (tenantId, _, _, _) = await SeedAsync(orgSlug, $"p-{Guid.NewGuid():N}"[..16], "u15");
        var newProjSlug = $"noslug-proj-{Guid.NewGuid():N}"[..20];

        var dir = CreateConfigDir();
        WriteModel(dir, "projects", $"{newProjSlug}.json5", new ProjectConfigModel
        {
            // No OrgSlug provided — cannot determine which org to create project under
            Name = "Should Not Be Created"
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            var resp = await TriggerSyncAsync(tenantId);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var issues = body.GetProperty("issues").EnumerateArray().ToList();
            Assert.Contains(issues, i =>
                i.GetProperty("severity").GetString() == "warning" &&
                i.GetProperty("message").GetString()!.Contains("orgSlug"));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var project = await db.Projects.FirstOrDefaultAsync(p =>
                p.Organization.TenantId == tenantId && p.Slug == newProjSlug);
            Assert.Null(project); // was not created
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_OrgMembers_StrictMode_UnknownUser_ReturnsError()
    {
        var orgSlug = $"strict2-org-{Guid.NewGuid():N}"[..20];
        var (tenantId, orgId, _, _) = await SeedAsync(orgSlug, $"p-{Guid.NewGuid():N}"[..16], "strictuser");

        var dir = CreateConfigDir();
        WriteModel(dir, "orgs", $"{orgSlug}.json5", new OrgConfigModel
        {
            Name = "Strict Name Update",
            Members = [new OrgMemberConfigModel { Username = "definitely_not_in_db", Role = OrgRole.Admin }]
        });

        try
        {
            await SetConfigRepoAsync(tenantId, dir, strict: true);
            var resp = await TriggerSyncAsync(tenantId);
            // In strict mode an unknown member is an error — sync returns 422.
            Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);

            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var issues = body.GetProperty("issues").EnumerateArray().ToList();
            Assert.Contains(issues, i =>
                i.GetProperty("severity").GetString() == "error" &&
                i.GetProperty("message").GetString()!.Contains("definitely_not_in_db"));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var org = await db.Organizations.FindAsync(orgId);
            Assert.NotNull(org);
            Assert.Equal("Strict Name Update", org.Name); // other fields still applied
            Assert.Equal(0, db.OrganizationMembers.Count(m => m.OrgId == orgId)); // no member added
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // -----------------------------------------------------------------------
    // Schema validation tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sync_OrgConfig_Json5WithComments_ParsedCorrectly()
    {
        var orgSlug = $"j5-org-{Guid.NewGuid():N}"[..20];
        var (tenantId, orgId, _, _) = await SeedAsync(orgSlug, $"p-{Guid.NewGuid():N}"[..16], "u12");

        var dir = CreateConfigDir();
        var orgsDir = Path.Combine(dir, "orgs");
        Directory.CreateDirectory(orgsDir);
        // JSON5 with line comments and trailing comma
        File.WriteAllText(Path.Combine(orgsDir, $"{orgSlug}.json5"), $$"""
            {
              // This is a JSON5 comment
              "slug": "{{orgSlug}}",
              "name": "JSON5 Org Name", // trailing comma below
              "maxConcurrentRunners": 3,
            }
            """);

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            var resp = await TriggerSyncAsync(tenantId);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var org = await db.Organizations.FindAsync(orgId);
            Assert.NotNull(org);
            Assert.Equal("JSON5 Org Name", org.Name);
            Assert.Equal(3, org.MaxConcurrentRunners);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Sync_OrgConfig_SchemaValidationError_FileSkippedAndErrorInResponse()
    {
        var orgSlug = $"schema-org-{Guid.NewGuid():N}"[..20];
        var (tenantId, orgId, _, _) = await SeedAsync(orgSlug, $"p-{Guid.NewGuid():N}"[..16], "u13");

        var dir = CreateConfigDir();
        var orgsDir = Path.Combine(dir, "orgs");
        Directory.CreateDirectory(orgsDir);
        // maxConcurrentRunners exceeds the [Range(0, 1000)] limit
        File.WriteAllText(Path.Combine(orgsDir, $"{orgSlug}.json5"), $$"""
            {
              "slug": "{{orgSlug}}",
              "name": "Schema Error Org",
              "maxConcurrentRunners": 9999
            }
            """);

        try
        {
            await SetConfigRepoAsync(tenantId, dir);
            var resp = await TriggerSyncAsync(tenantId);
            // Sync returns 200 even with validation errors (partial apply)
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var issues = body.GetProperty("issues").EnumerateArray().ToList();
            Assert.Contains(issues, i =>
                i.GetProperty("severity").GetString() == "error" &&
                i.GetProperty("message").GetString()!.Contains("maxConcurrentRunners"));

            // Validation error means the file was skipped — org name unchanged
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var org = await db.Organizations.FindAsync(orgId);
            Assert.NotNull(org);
            Assert.Equal("Cfg Org", org.Name); // unchanged
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
