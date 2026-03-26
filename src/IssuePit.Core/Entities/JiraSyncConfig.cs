using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>
/// Stores the Jira synchronisation configuration for a project.
/// One record per project (created on first save).
/// </summary>
[Table("jira_sync_configs")]
public class JiraSyncConfig
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Jira base URL, e.g. <c>https://your-company.atlassian.net</c>.
    /// </summary>
    [MaxLength(500)]
    public string? JiraBaseUrl { get; set; }

    /// <summary>
    /// Jira project key (e.g. <c>PROJ</c>).
    /// </summary>
    [MaxLength(100)]
    public string? JiraProjectKey { get; set; }

    /// <summary>
    /// Email address of the Jira user whose API token is used for authentication.
    /// </summary>
    [MaxLength(300)]
    public string? JiraEmail { get; set; }

    /// <summary>
    /// ID of the API key record (<see cref="ApiKey"/>) that holds the Jira API token.
    /// The API key must have <see cref="ApiKeyProvider.Jira"/> as its provider.
    /// </summary>
    public Guid? ApiKeyId { get; set; }

    [ForeignKey(nameof(ApiKeyId))]
    public ApiKey? ApiKey { get; set; }

    /// <summary>Controls when automatic sync is triggered.</summary>
    public JiraSyncTriggerMode TriggerMode { get; set; } = JiraSyncTriggerMode.Off;

    /// <summary>
    /// When <c>true</c>, only issues that have a parent (epic, story, sub-task parent)
    /// set in Jira will be imported. Issues without a parent are skipped.
    /// </summary>
    public bool OnlyImportWithParent { get; set; } = false;

    /// <summary>
    /// When <c>true</c>, Jira issue comments are imported as IssuePit issue comments.
    /// </summary>
    public bool ImportComments { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
