using System.Data.Common;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Middleware;

public class TenantMiddleware(RequestDelegate next, IConfiguration configuration)
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

            if (tenantContext.CurrentTenant is null)
            {
                var defaultTenantId = configuration["DefaultTenantId"];
                if (!string.IsNullOrEmpty(defaultTenantId) && Guid.TryParse(defaultTenantId, out var defaultTid))
                {
                    tenantContext.CurrentTenant = await db.Tenants.FindAsync(defaultTid);
                }
            }
        }
        catch (DbException)
        {
            // The tenants table may not exist yet (pre-migration); leave CurrentTenant null.
        }

        await next(context);
    }
}
