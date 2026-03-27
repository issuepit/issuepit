using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>
/// Configures a periodic scheduled run of a kanban orchestrator agent for a specific board.
/// The background service checks this table every minute; it only launches an agent session when
/// the board state has changed since the last run (detected via <see cref="LastBoardStateHash"/>),
/// preventing redundant runs on a static board.
/// </summary>
[Table("kanban_orchestrator_schedules")]
public class KanbanOrchestratorSchedule
{
    [Key]
    public Guid Id { get; set; }

    public Guid BoardId { get; set; }

    [ForeignKey(nameof(BoardId))]
    public KanbanBoard Board { get; set; } = null!;

    /// <summary>The orchestrator agent to run when the schedule fires.</summary>
    public Guid AgentId { get; set; }

    [ForeignKey(nameof(AgentId))]
    public Agent Agent { get; set; } = null!;

    /// <summary>When false the schedule is paused and no sessions are started.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>How often (in minutes) to check whether the board has changed and run the orchestrator.</summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Timestamp of the last time this schedule was actually triggered.
    /// Null if it has never run.
    /// </summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// SHA-256 hash of the board state at the time of the last run.
    /// Used to detect whether any issue has moved, changed status, or been added/removed since
    /// the last orchestration cycle. If the current hash equals this value the run is skipped.
    /// </summary>
    [MaxLength(64)]
    public string? LastBoardStateHash { get; set; }

    /// <summary>Agent session started by the most recent scheduled (or manual) trigger. Null until the first run.</summary>
    public Guid? LastSessionId { get; set; }

    [ForeignKey(nameof(LastSessionId))]
    public AgentSession? LastSession { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
