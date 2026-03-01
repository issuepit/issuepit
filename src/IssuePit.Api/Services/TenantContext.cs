using IssuePit.Core.Entities;

namespace IssuePit.Api.Services;

public class TenantContext
{
    public Tenant? CurrentTenant { get; set; }

    /// <summary>The authenticated local user resolved from the session cookie.</summary>
    public User? CurrentUser { get; set; }
}
