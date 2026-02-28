using IssuePit.Core.Entities;

namespace IssuePit.Api.Services;

public class TenantContext
{
    public Tenant? CurrentTenant { get; set; }
}
