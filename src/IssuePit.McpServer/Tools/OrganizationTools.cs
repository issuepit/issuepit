using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace IssuePit.McpServer.Tools;

[McpServerToolType]
public class OrganizationTools(IssuePitApiClient api, IOptions<McpServerOptions> options)
{
    private McpServerOptions Opts => options.Value;

    [McpServerTool, Description("List all organizations for the current tenant.")]
    public async Task<string> ListOrganizations(CancellationToken ct = default)
    {
        ToolGuard.EnforceNotAgentMode(Opts, "ListOrganizations");
        ToolGuard.EnforceNotEnhanceMode(Opts, "ListOrganizations");
        var result = await api.GetAsync<object>("/api/orgs", ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Get details of a specific organization by its ID.")]
    public async Task<string> GetOrganization(
        [Description("The organization ID (GUID).")] Guid id,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotEnhanceMode(Opts, "GetOrganization");
        var result = await api.GetAsync<object>($"/api/orgs/{id}", ct);
        return ToolSerializer.Serialize(result);
    }
}
