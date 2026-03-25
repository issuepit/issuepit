using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>
/// Records the full runtime history of a single Hetzner Cloud server lifecycle.
/// One row per server (written when the server is deleted or marked as deleted).
/// Used to build cost dashboards and audit trails.
/// </summary>
[Table("hetzner_server_runtime_histories")]
public class HetznerServerRuntimeHistory
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Reference to the server record (kept even after the server is deleted).</summary>
    public Guid HetznerServerId { get; set; }

    [ForeignKey(nameof(HetznerServerId))]
    public HetznerServer HetznerServer { get; set; } = null!;

    public Guid OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization Organization { get; set; } = null!;

    /// <summary>Hetzner server type at the time of provisioning (e.g. "cx22", "cx32").</summary>
    [Required, MaxLength(100)]
    public string ServerType { get; set; } = string.Empty;

    /// <summary>Datacenter location (e.g. "nbg1").</summary>
    [MaxLength(50)]
    public string Location { get; set; } = string.Empty;

    /// <summary>When the server was created/provisioned in IssuePit.</summary>
    public DateTime ProvisionedAt { get; set; }

    /// <summary>When the server became ready for jobs (cloud-init finished).</summary>
    public DateTime? ReadyAt { get; set; }

    /// <summary>When the server was deleted or marked as deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Total runtime in seconds from provisioning to deletion.</summary>
    public int? TotalRuntimeSeconds { get; set; }

    /// <summary>
    /// Total uptime in seconds from first-ready to deletion.
    /// This is the billable window (Hetzner bills by the hour from server creation).
    /// </summary>
    public int? BillableSeconds { get; set; }

    /// <summary>Total number of CI/CD runs executed on this server over its lifetime.</summary>
    public int TotalJobCount { get; set; } = 0;

    /// <summary>How long the initial cloud-init setup took in seconds.</summary>
    public int? SetupDurationSeconds { get; set; }

    /// <summary>
    /// Estimated cost in EUR-cents based on server type and billable time.
    /// Null when the price data is unavailable.
    /// </summary>
    public int? EstimatedCostEuroCents { get; set; }

    /// <summary>Peak CPU load percent recorded during this server's lifetime.</summary>
    public double? PeakCpuLoadPercent { get; set; }

    /// <summary>Peak RAM usage in megabytes recorded during this server's lifetime.</summary>
    public int? PeakRamUsedMb { get; set; }

    /// <summary>When this history record was written.</summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
