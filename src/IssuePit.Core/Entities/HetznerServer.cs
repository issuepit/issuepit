using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>
/// Tracks a Hetzner Cloud server provisioned for CI/CD or agent workloads.
/// One row per server — updated in-place as the server lifecycle progresses.
/// </summary>
[Table("hetzner_servers")]
public class HetznerServer
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Numeric server ID returned by the Hetzner Cloud API.</summary>
    public long HetznerServerId { get; set; }

    /// <summary>Human-readable server name (e.g. "issuepit-cicd-abc123").</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Primary IPv4 address. Null when the server was created with IPv6-only.</summary>
    [MaxLength(45)]
    public string? Ipv4Address { get; set; }

    /// <summary>Primary IPv6 address / CIDR block (e.g. "2a01:4f8:1:2::1/64").</summary>
    [MaxLength(50)]
    public string? Ipv6Address { get; set; }

    /// <summary>Hetzner server type used (e.g. "cx22", "cpx31").</summary>
    [MaxLength(50)]
    public string ServerType { get; set; } = string.Empty;

    /// <summary>Hetzner datacenter/location where the server was created (e.g. "nbg1").</summary>
    [MaxLength(50)]
    public string Location { get; set; } = string.Empty;

    public HetznerServerStatus Status { get; set; } = HetznerServerStatus.Provisioning;

    /// <summary>Number of CI/CD runs currently executing on this server.</summary>
    public int ActiveRunCount { get; set; }

    /// <summary>Total number of CI/CD runs that have been executed on this server (lifetime).</summary>
    public int TotalRunCount { get; set; }

    /// <summary>Most recent CPU load (0–100). Null when not yet collected.</summary>
    public double? CpuLoadPercent { get; set; }

    /// <summary>Most recent RAM usage in bytes. Null when not yet collected.</summary>
    public long? RamUsedBytes { get; set; }

    /// <summary>Total RAM on the server in bytes. Null when not yet collected.</summary>
    public long? RamTotalBytes { get; set; }

    /// <summary>Timestamp when the server was created in Hetzner Cloud.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when the last run finished and the server became idle again.</summary>
    public DateTime? LastIdleAt { get; set; }

    /// <summary>Timestamp of the last successful metrics collection.</summary>
    public DateTime? MetricsLastCollectedAt { get; set; }

    /// <summary>Time in seconds taken to initialise the server (cloud-init + Docker pull). Null until complete.</summary>
    public int? SetupTimeSeconds { get; set; }

    /// <summary>Optional error message captured during provisioning or execution.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The organisation that owns this server (used for API-key resolution).
    /// Null for globally-shared servers provisioned via appsettings credentials.
    /// </summary>
    public Guid? OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization? Organization { get; set; }
}
