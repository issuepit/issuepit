using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>A single log line emitted during a <see cref="GitRepoAutoFetchRun"/>.</summary>
[Table("git_repo_auto_fetch_run_logs")]
public class GitRepoAutoFetchRunLog
{
    [Key]
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    [ForeignKey(nameof(RunId))]
    public GitRepoAutoFetchRun Run { get; set; } = null!;

    public GitHubSyncLogLevel Level { get; set; } = GitHubSyncLogLevel.Info;

    [Required]
    public string Message { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
