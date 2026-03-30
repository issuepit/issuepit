using IssuePit.Notes.Api.Services;

namespace IssuePit.Notes.Api.Middleware;

/// <summary>
/// Resolves the current tenant from the X-Tenant-Id request header.
/// The Notes service trusts the header value since tenant authentication is handled
/// by the main IssuePit API gateway or the frontend.
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
