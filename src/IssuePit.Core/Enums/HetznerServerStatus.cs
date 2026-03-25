namespace IssuePit.Core.Enums;

public enum HetznerServerStatus
{
    /// <summary>Server is being provisioned via the Hetzner Cloud API.</summary>
    Provisioning,
    /// <summary>Server exists in Hetzner but cloud-init (Docker setup, image pull) is still running.</summary>
    Initializing,
    /// <summary>Server is ready and idle — no active jobs.</summary>
    Idle,
    /// <summary>Server is running one or more CI/CD jobs.</summary>
    Running,
    /// <summary>Server is in the idle-timeout window and will be deleted soon if no new jobs arrive.</summary>
    SpinningDown,
    /// <summary>Deletion has been requested (API call made or self-destruct script triggered).</summary>
    Deleting,
    /// <summary>Server has been deleted from Hetzner Cloud.</summary>
    Deleted,
    /// <summary>Provisioning or setup failed.</summary>
    Failed,
}
