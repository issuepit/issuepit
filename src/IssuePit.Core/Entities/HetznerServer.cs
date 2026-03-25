using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>
/// Tracks a Hetzner Cloud server provisioned by IssuePit for CI/CD or agent workloads.
/// One server may host multiple concurrent runs (subject to scheduling limits).
/// </summary>
[Table("hetzner_servers")]
public class HetznerServer
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Hetzner Cloud numeric server ID returned by the API on creation.</summary>
    public long HetznerServerId { get; set; }

    /// <summary>Organization that owns this server (and holds the Hetzner API key).</summary>
    public Guid OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization Organization { get; set; } = null!;

    /// <summary>Human-readable Hetzner server name (also set on the cloud server itself).</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Hetzner server type / size (e.g. "cx11", "cx21", "cx31").</summary>
    [MaxLength(100)]
    public string ServerType { get; set; } = "cx22";

    /// <summary>Hetzner datacenter location (e.g. "nbg1", "fsn1", "hel1").</summary>
    [MaxLength(50)]
    public string Location { get; set; } = "nbg1";

    /// <summary>IPv6 address assigned by Hetzner (primary networking; IPv4 is optional/skipped by default).</summary>
    [MaxLength(100)]
    public string? Ipv6Address { get; set; }

    /// <summary>IPv4 address, populated only when IPv4 is explicitly requested.</summary>
    [MaxLength(50)]
    public string? Ipv4Address { get; set; }

    public HetznerServerStatus Status { get; set; } = HetznerServerStatus.Provisioning;

    /// <summary>When the server was created/requested in IssuePit.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the server became ready (SSH reachable, Docker running).</summary>
    public DateTime? ReadyAt { get; set; }

    /// <summary>When the last workload finished on this server (used for idle-timeout calculation).</summary>
    public DateTime? LastJobEndedAt { get; set; }

    /// <summary>When the server was deleted or requested for deletion.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Number of CI/CD runs currently in progress on this server.</summary>
    public int ActiveJobCount { get; set; } = 0;

    /// <summary>Total number of CI/CD runs that have run on this server since creation.</summary>
    public int TotalJobCount { get; set; } = 0;

    /// <summary>Most recent CPU load average (0–100). Null when no metrics have been collected.</summary>
    public double? CpuLoadPercent { get; set; }

    /// <summary>Most recent RAM usage in megabytes. Null when no metrics have been collected.</summary>
    public int? RamUsedMb { get; set; }

    /// <summary>Total RAM of the server type in megabytes. Set at provisioning time.</summary>
    public int? RamTotalMb { get; set; }

    /// <summary>How long the initial cloud-init setup took in seconds (Docker install, image pull, etc.).</summary>
    public int? SetupDurationSeconds { get; set; }

    /// <summary>
    /// SSH private key (PEM) generated for this server. Stored so the worker can reconnect.
    /// Encrypted at rest in production (prefixed with "plain:" for dev/testing).
    /// </summary>
    public string? SshPrivateKey { get; set; }

    /// <summary>Hetzner SSH key ID uploaded to the cloud project for this server.</summary>
    public long? HetznerSshKeyId { get; set; }

    /// <summary>Error message if provisioning or deletion failed.</summary>
    public string? LastError { get; set; }
}
