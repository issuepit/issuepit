using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace IssuePit.Core.Entities;

/// <summary>
/// Represents an external issue-tracking system linked to an IssuePit project.
/// Each record describes one external source (e.g. a GitHub repository or a Jira project)
/// and stores enough information to build deep-links and format external issue IDs.
/// </summary>
[Table("issue_external_sources")]
public class IssueExternalSource
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The type of external tracker. Standard values: <c>"github"</c>, <c>"jira"</c>.
    /// </summary>
    [Required, MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Project key or slug in the external system (e.g. <c>"PROJ"</c> for a Jira project).
    /// Null for GitHub because GitHub repositories have no project-level issue prefix.
    /// </summary>
    [MaxLength(200)]
    public string? Slug { get; set; }

    /// <summary>
    /// Base URL of the external repository or project
    /// (e.g. <c>"https://github.com/org/repo"</c> or <c>"https://mycompany.atlassian.net/jira/software/projects/PROJ"</c>).
    /// </summary>
    [MaxLength(500)]
    public string? Url { get; set; }

    /// <summary>The IssuePit project this external source belongs to.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Navigation property — not serialised to avoid cycles.</summary>
    [ForeignKey(nameof(ProjectId))]
    [JsonIgnore]
    public Project? Project { get; set; }
}
