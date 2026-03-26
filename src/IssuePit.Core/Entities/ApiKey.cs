using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("api_keys")]
public class ApiKey
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization Organization { get; set; } = null!;

    // Optional narrower scopes. The resolver picks the most specific non-null scope first:
    // project > team > user > org (null = org-level key).

    public Guid? ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    public Guid? TeamId { get; set; }

    [ForeignKey(nameof(TeamId))]
    public Team? Team { get; set; }

    public Guid? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public ApiKeyProvider Provider { get; set; }

    /// <summary>Encrypted value — never returned in API responses.</summary>
    [Required]
    public string EncryptedValue { get; set; } = string.Empty;

    /// <summary>
    /// For Jira API keys: the Jira instance base URL, e.g. <c>https://your-company.atlassian.net</c>.
    /// </summary>
    [MaxLength(500)]
    public string? JiraBaseUrl { get; set; }

    /// <summary>
    /// For Jira API keys: the email address of the Jira user whose token this is.
    /// </summary>
    [MaxLength(300)]
    public string? JiraEmail { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }
}
