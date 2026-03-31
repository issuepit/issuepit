using IssuePit.Notes.Api.Services;

namespace IssuePit.Notes.Api.Middleware;

/// <summary>
/// Resolves the tenant ID for the current request from the X-Tenant-Id header.
/// The Notes service is decoupled from the main IssuePit database, so it uses a
/// header-based approach rather than looking up tenants from the main DB.
/// </summary>
public class NotesTenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, NotesTenantContext tenantContext)
    {
        var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        if (!string.IsNullOrEmpty(tenantId) && Guid.TryParse(tenantId, out var tid))
        {
            tenantContext.TenantId = tid;
        }

        await next(context);
    }
}
