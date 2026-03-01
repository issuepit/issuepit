using System.Data.Common;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Middleware;

public class TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IssuePitDbContext db, TenantContext tenantContext)
    {
        try
        {
            string? tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();

            if (!string.IsNullOrEmpty(tenantId) && Guid.TryParse(tenantId, out var tid))
            {
                tenantContext.CurrentTenant = await db.Tenants.FindAsync(tid);
            }
            else
            {
                var hostname = context.Request.Host.Host;
                tenantContext.CurrentTenant = await db.Tenants
                    .FirstOrDefaultAsync(t => t.Hostname == hostname);
            }
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            // Default tenant: uses the default DB and leaves CurrentTenant as null.
            // This occurs locally and in tests where the tenants table may not exist yet.
            logger.LogWarning(ex, "Tenant lookup failed; using default tenant (null).");
            tenantContext.CurrentTenant = null;
        }

        // Resolve the authenticated user from the session claim set by cookie auth.
        var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
        {
            tenantContext.CurrentUser = await db.Users.FindAsync(userId);
        }

        await next(context);
    }
}
