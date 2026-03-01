using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("org_members")]
public class OrganizationMember
{
    public Guid OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization Organization { get; set; } = null!;

    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    public OrgRole Role { get; set; } = OrgRole.Member;
}
