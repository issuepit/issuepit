using IssuePit.Notes.Api.Services;

namespace IssuePit.Notes.Api.Middleware;

/// <summary>
/// Resolves the current tenant from the X-Tenant-Id request header.
/// <para>
/// <b>Security note:</b> This service trusts the header value without further validation.
/// It must only be exposed behind the main IssuePit API gateway or a reverse proxy that
/// authenticates the tenant before forwarding requests. Direct public exposure would allow
/// any client to impersonate any tenant by sending an arbitrary header.
/// </para>
/// </summary>
public class NotesTenantMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, NotesTenantContext tenantContext)
    {
        var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (!string.IsNullOrEmpty(tenantId) && Guid.TryParse(tenantId, out var tid))
        {
            tenantContext.TenantId = tid;
        }

        return next(context);
    }
}
