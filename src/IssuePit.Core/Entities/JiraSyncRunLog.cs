using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>A single log line emitted during a <see cref="JiraSyncRun"/>.</summary>
[Table("jira_sync_run_logs")]
public class JiraSyncRunLog
{
    [Key]
    public Guid Id { get; set; }

    public Guid SyncRunId { get; set; }

    [ForeignKey(nameof(SyncRunId))]
    public JiraSyncRun SyncRun { get; set; } = null!;

    public GitHubSyncLogLevel Level { get; set; } = GitHubSyncLogLevel.Info;

    [Required]
    public string Message { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
