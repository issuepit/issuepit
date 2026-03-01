using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("team_members")]
public class TeamMember
{
    public Guid TeamId { get; set; }

    [ForeignKey(nameof(TeamId))]
    public Team Team { get; set; } = null!;

    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
