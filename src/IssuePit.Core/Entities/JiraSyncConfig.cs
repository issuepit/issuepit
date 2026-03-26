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
    /// Jira project key (e.g. <c>PROJ</c>).
    /// </summary>
    [MaxLength(100)]
    public string? JiraProjectKey { get; set; }

    /// <summary>
    /// ID of the API key record (<see cref="ApiKey"/>) that holds the Jira API token.
    /// The API key must have <see cref="ApiKeyProvider.Jira"/> as its provider and must carry
    /// <c>JiraBaseUrl</c> and <c>JiraEmail</c> on the key itself.
    /// </summary>
    public Guid? ApiKeyId { get; set; }

    [ForeignKey(nameof(ApiKeyId))]
    public ApiKey? ApiKey { get; set; }

    /// <summary>Controls when automatic sync is triggered.</summary>
    public JiraSyncTriggerMode TriggerMode { get; set; } = JiraSyncTriggerMode.Off;

    /// <summary>
    /// Comma-separated list of Jira issue keys whose direct children (sub-tasks, child issues) should be imported.
    /// When <c>null</c> or empty, all issues in the project are imported.
    /// Example: <c>PROJ-1,PROJ-2</c>.
    /// </summary>
    [MaxLength(2000)]
    public string? ParentIssueKeys { get; set; }

    /// <summary>
    /// When <c>true</c>, Jira issue comments are imported as IssuePit issue comments.
    /// </summary>
    public bool ImportComments { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
