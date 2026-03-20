using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("similar_issue_run_logs")]
public class SimilarIssueRunLog
{
    [Key]
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    [ForeignKey(nameof(RunId))]
    public SimilarIssueRun Run { get; set; } = null!;
    public GitHubSyncLogLevel Level { get; set; }
    [Required]
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
