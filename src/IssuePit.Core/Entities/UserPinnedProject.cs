using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>Records a user's personal project pin so that pinned projects appear at the top of the sidebar and dashboard.</summary>
[Table("user_pinned_projects")]
public class UserPinnedProject
{
    [Key]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    public DateTime PinnedAt { get; set; } = DateTime.UtcNow;
}
