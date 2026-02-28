using IssuePit.Api.Services;
using IssuePit.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Middleware;

public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IssuePitDbContext db, TenantContext tenantContext)
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

        await next(context);
    }
}
