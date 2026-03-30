namespace IssuePit.Notes.Api.Services;

/// <summary>
/// Scoped service that holds the resolved tenant ID for the current request.
/// The Notes service uses a simplified tenant model — it stores tenant IDs directly
/// rather than loading full Tenant entities from the main DB.
/// </summary>
public class NotesTenantContext
{
    public Guid? TenantId { get; set; }
}
