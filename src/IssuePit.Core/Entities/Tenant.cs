using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("tenants")]
public class Tenant
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(253)]
    public string Hostname { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? DatabaseConnectionString { get; set; }

    /// <summary>Git repository URL or local filesystem path to the infrastructure-as-code config directory.</summary>
    [MaxLength(1000)]
    public string? ConfigRepoUrl { get; set; }

    /// <summary>Optional PAT or access token for authenticating with the config git repository.</summary>
    [MaxLength(1000)]
    public string? ConfigRepoToken { get; set; }

    /// <summary>Optional username used together with <see cref="ConfigRepoToken"/> for HTTP basic auth.</summary>
    [MaxLength(200)]
    public string? ConfigRepoUsername { get; set; }

    /// <summary>
    /// When <c>true</c>, member imports fail if a referenced username is not found in the database.
    /// When <c>false</c> (default), unknown usernames are silently skipped.
    /// </summary>
    public bool ConfigStrictMode { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
