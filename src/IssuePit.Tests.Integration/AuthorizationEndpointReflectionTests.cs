using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using IssuePit.Core;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class AuthorizationEndpointReflectionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task AllAdminEndpoints_WithNonAdminToken_Return403()
    {
        var client = await CreateTokenClientAsync(isAdmin: false);
        var defaultGuid = Guid.NewGuid();

        var endpoints = GetEndpoints(a =>
            a.RelativePath?.StartsWith("api/admin/", StringComparison.OrdinalIgnoreCase) == true);

        var failures = new List<string>();
        foreach (var endpoint in endpoints)
        {
            using var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), BuildPath(endpoint.Path, defaultGuid));
            if (RequiresBody(endpoint.Method))
                request.Content = JsonContent.Create(new { });

            var response = await client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.Forbidden)
                failures.Add($"{endpoint.Method} /{endpoint.Path} -> {(int)response.StatusCode}");
        }

        Assert.True(failures.Count == 0, $"Expected all admin endpoints to return 403.{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    [Fact]
    public async Task ProjectScopedToken_ProjectEndpointsOutsideScope_Return403()
    {
        var allowedProjectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var client = await CreateTokenClientAsync(projectScopeId: allowedProjectId);
        var defaultGuid = Guid.NewGuid();

        var endpoints = GetEndpoints(a =>
            a.RelativePath?.Contains("api/projects/{projectId", StringComparison.OrdinalIgnoreCase) == true);

        var failures = new List<string>();
        foreach (var endpoint in endpoints)
        {
            using var request = new HttpRequestMessage(
                new HttpMethod(endpoint.Method),
                BuildPath(endpoint.Path, defaultGuid, projectId: otherProjectId));
            if (RequiresBody(endpoint.Method))
                request.Content = JsonContent.Create(new { });

            var response = await client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.Forbidden)
                failures.Add($"{endpoint.Method} /{endpoint.Path} -> {(int)response.StatusCode}");
        }

        Assert.True(failures.Count == 0, $"Expected all out-of-scope project endpoints to return 403.{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    [Fact]
    public async Task OrgScopedToken_OrgEndpointsOutsideScope_Return403()
    {
        var allowedOrgId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var client = await CreateTokenClientAsync(orgScopeId: allowedOrgId);
        var defaultGuid = Guid.NewGuid();

        var endpoints = GetEndpoints(a =>
            a.RelativePath?.Contains("api/orgs/{orgId", StringComparison.OrdinalIgnoreCase) == true);

        var failures = new List<string>();
        foreach (var endpoint in endpoints)
        {
            using var request = new HttpRequestMessage(
                new HttpMethod(endpoint.Method),
                BuildPath(endpoint.Path, defaultGuid, orgId: otherOrgId));
            if (RequiresBody(endpoint.Method))
                request.Content = JsonContent.Create(new { });

            var response = await client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.Forbidden)
                failures.Add($"{endpoint.Method} /{endpoint.Path} -> {(int)response.StatusCode}");
        }

        Assert.True(failures.Count == 0, $"Expected all out-of-scope org endpoints to return 403.{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private async Task<HttpClient> CreateTokenClientAsync(bool isAdmin = false, Guid? projectScopeId = null, Guid? orgScopeId = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var rawToken = $"scope-{Guid.NewGuid():N}";

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "AuthScope", Hostname = $"scope-{tenantId}.test" });
        db.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            Username = $"scope-user-{Guid.NewGuid():N}"[..24],
            Email = $"scope-{Guid.NewGuid():N}@test.local",
            IsAdmin = isAdmin
        });
        db.McpTokens.Add(new McpToken
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            ProjectId = projectScopeId,
            OrgId = orgScopeId,
            Name = "scope-token",
            KeyHash = HashHelper.ComputeSha256Hex(rawToken)
        });
        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Mcp-Token", rawToken);
        return client;
    }

    private IEnumerable<(string Method, string Path)> GetEndpoints(Func<ApiDescription, bool> predicate)
    {
        var provider = factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>();
        return provider.ApiDescriptionGroups.Items
            .SelectMany(g => g.Items)
            .Where(predicate)
            .Where(a => !string.IsNullOrWhiteSpace(a.RelativePath) && !string.IsNullOrWhiteSpace(a.HttpMethod))
            .Select(a => (a.HttpMethod!, a.RelativePath!.Split('?')[0]))
            .Distinct()
            .ToList();
    }

    private static bool RequiresBody(string method) =>
        method.Equals(HttpMethod.Post.Method, StringComparison.OrdinalIgnoreCase)
        || method.Equals(HttpMethod.Put.Method, StringComparison.OrdinalIgnoreCase)
        || method.Equals(HttpMethod.Patch.Method, StringComparison.OrdinalIgnoreCase);

    private static string BuildPath(string template, Guid defaultGuid, Guid? projectId = null, Guid? orgId = null)
    {
        var path = Regex.Replace(template, "\\{([^}]+)\\}", m =>
        {
            var token = m.Groups[1].Value;
            var name = token.Split(':', StringSplitOptions.RemoveEmptyEntries)[0];
            if (name.Equals("projectId", StringComparison.OrdinalIgnoreCase))
                return (projectId ?? defaultGuid).ToString();
            if (name.Equals("orgId", StringComparison.OrdinalIgnoreCase))
                return (orgId ?? defaultGuid).ToString();
            if (token.Contains("int", StringComparison.OrdinalIgnoreCase))
                return "1";
            return defaultGuid.ToString();
        });

        return $"/{path}";
    }
}
