using Confluent.Kafka;
using IssuePit.Api.Controllers;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IssuePit.Api.Services;

/// <summary>
/// Periodically checks all enabled <see cref="KanbanOrchestratorSchedule"/> entries and launches
/// an orchestrator agent session for any board whose state has changed since the last run.
/// </summary>
/// <remarks>
/// <para>
/// Board state is hashed via <see cref="KanbanController.ComputeBoardStateHashAsync"/> —
/// a SHA-256 digest of all issues' id, status, and kanban rank.  If the hash matches the value
/// stored from the last run, the board is considered unchanged and the orchestrator is NOT started
/// (preventing redundant no-op runs on a static board).
/// </para>
/// <para>
/// The service wakes up every 5 minutes (configurable via <c>Kanban:OrchestratorCheckIntervalSeconds</c>)
/// and checks whether each schedule's <c>IntervalMinutes</c> has elapsed since <c>LastRunAt</c>.
/// Using a 5-minute poll interval avoids unnecessary CPU/DB work while still firing schedules
/// within one poll window of their configured interval.
/// </para>
/// </remarks>
public class KanbanOrchestratorBackgroundService(
    ILogger<KanbanOrchestratorBackgroundService> logger,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration)
    : PeriodicBackgroundService(
        logger,
        TimeSpan.FromSeconds(configuration.GetValue("Kanban:OrchestratorCheckIntervalSeconds", 300)),
        startupDelay: TimeSpan.FromSeconds(60))
{
    protected override async Task ExecuteTickAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var producer = scope.ServiceProvider.GetRequiredService<IProducer<string, string>>();

        var now = DateTime.UtcNow;

        // Load all enabled schedules whose interval has elapsed
        var schedules = await db.KanbanOrchestratorSchedules
            .Include(s => s.Board).ThenInclude(b => b.Project)
            .Where(s => s.IsEnabled)
            .ToListAsync(stoppingToken);

        foreach (var schedule in schedules)
        {
            if (stoppingToken.IsCancellationRequested) break;

            // Check if enough time has elapsed since last run
            if (schedule.LastRunAt.HasValue &&
                (now - schedule.LastRunAt.Value).TotalMinutes < schedule.IntervalMinutes)
                continue;

            try
            {
                await RunScheduleAsync(db, producer, schedule, now, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "KanbanOrchestrator: error processing schedule {ScheduleId} for board {BoardId}",
                    schedule.Id, schedule.BoardId);
            }
        }
    }

    private async Task RunScheduleAsync(
        IssuePitDbContext db,
        IProducer<string, string> producer,
        KanbanOrchestratorSchedule schedule,
        DateTime now,
        CancellationToken ct)
    {
        var currentHash = await KanbanController.ComputeBoardStateHashAsync(db, schedule.BoardId);

        if (currentHash == schedule.LastBoardStateHash)
        {
            logger.LogInformation(
                "KanbanOrchestrator: board {BoardId} unchanged (hash={Hash}) — skipping",
                schedule.BoardId, currentHash);
            return;
        }

        logger.LogInformation(
            "KanbanOrchestrator: board {BoardId} changed — launching session for agent {AgentId}",
            schedule.BoardId, schedule.AgentId);

        var session = new AgentSession
        {
            Id = Guid.NewGuid(),
            AgentId = schedule.AgentId,
            IssueId = null,
            ProjectId = schedule.Board.ProjectId,
            Status = AgentSessionStatus.Pending,
        };
        db.AgentSessions.Add(session);
        schedule.LastRunAt = now;
        schedule.LastBoardStateHash = currentHash;
        schedule.LastSessionId = session.Id;
        await db.SaveChangesAsync(ct);

        try
        {
            await producer.ProduceAsync("issue-assigned", new Message<string, string>
            {
                Key = schedule.Board.ProjectId.ToString(),
                Value = JsonSerializer.Serialize(new
                {
                    Id = Guid.Empty,
                    schedule.Board.ProjectId,
                    SessionId = session.Id,
                    OrchestratorMode = true,
                    BoardId = schedule.BoardId,
                })
            });

            logger.LogInformation(
                "KanbanOrchestrator: queued session {SessionId} for board {BoardId}",
                session.Id, schedule.BoardId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "KanbanOrchestrator: failed to publish Kafka message for session {SessionId}",
                session.Id);
            session.Status = AgentSessionStatus.Failed;
            session.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
}
