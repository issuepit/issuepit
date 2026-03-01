using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>Maps a <see cref="GitHubIdentity"/> to an <see cref="Organization"/>.</summary>
[Table("github_identity_orgs")]
public class GitHubIdentityOrg
{
    public Guid GitHubIdentityId { get; set; }

    [ForeignKey(nameof(GitHubIdentityId))]
    public GitHubIdentity GitHubIdentity { get; set; } = null!;

    public Guid OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization Organization { get; set; } = null!;
}
