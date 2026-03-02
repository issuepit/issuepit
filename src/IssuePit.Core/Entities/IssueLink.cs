using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("issue_links")]
public class IssueLink
{
    [Key]
    public Guid Id { get; set; }

    public Guid IssueId { get; set; }

    [ForeignKey(nameof(IssueId))]
    public Issue? Issue { get; set; }

    public Guid TargetIssueId { get; set; }

    [ForeignKey(nameof(TargetIssueId))]
    public Issue? TargetIssue { get; set; }

    public IssueLinkType LinkType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
