using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>Maps a <see cref="GitHubIdentity"/> to a <see cref="Project"/>.</summary>
[Table("github_identity_projects")]
public class GitHubIdentityProject
{
    public Guid GitHubIdentityId { get; set; }

    [ForeignKey(nameof(GitHubIdentityId))]
    public GitHubIdentity GitHubIdentity { get; set; } = null!;

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;
}
