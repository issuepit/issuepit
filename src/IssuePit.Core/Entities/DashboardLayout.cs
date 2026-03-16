using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>
/// Persisted dashboard layout — covers user-personal presets, named shared templates,
/// and per-project default layouts.
/// </summary>
[Table("dashboard_layouts")]
public class DashboardLayout
{
    [Key]
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Human-readable name for the layout (e.g. "Sprint view", "Minimal").</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>'main' for the global dashboard, 'project' for a project dashboard.</summary>
    [Required, MaxLength(20)]
    public string DashboardType { get; set; } = string.Empty;

    /// <summary>
    /// 'user' – personal layout for a single user.
    /// 'project_default' – default layout for all project members who have no personal layout.
    /// 'shared' – named template visible to all tenant members.
    /// </summary>
    [Required, MaxLength(20)]
    public string Scope { get; set; } = string.Empty;

    /// <summary>Set when Scope is 'project_default' or DashboardType is 'project'.</summary>
    public Guid? ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>Set when Scope is 'user'.</summary>
    public Guid? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>The full serialised layout JSON (order + configs).</summary>
    [Required]
    public string LayoutJson { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
