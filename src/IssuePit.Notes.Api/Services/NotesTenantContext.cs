namespace IssuePit.Notes.Api.Services;

/// <summary>
/// Scoped service holding the current request's tenant identity.
/// The Notes service is decoupled from the main IssuePit database, so tenant resolution
/// relies on the X-Tenant-Id header only. The main API gateway (or frontend) is responsible
/// for authenticating the tenant before forwarding requests.
/// </summary>
public class NotesTenantContext
{
    /// <summary>The tenant ID resolved from the request header.</summary>
    public Guid? TenantId { get; set; }
}
