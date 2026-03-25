namespace IssuePit.Core.Enums;

public enum HetznerServerStatus
{
    /// <summary>Server is being provisioned via the Hetzner Cloud API.</summary>
    Provisioning,
    /// <summary>Server is booting and cloud-init is running (installing Docker, pulling images, etc.).</summary>
    Initializing,
    /// <summary>Server is ready and idle — no workloads are currently running on it.</summary>
    Idle,
    /// <summary>Server is actively running one or more CI/CD jobs.</summary>
    Running,
    /// <summary>Server is in the spin-down cooldown window before deletion.</summary>
    Draining,
    /// <summary>Server has been deleted from Hetzner Cloud.</summary>
    Deleted,
    /// <summary>An error occurred during provisioning or execution.</summary>
    Error,
}
