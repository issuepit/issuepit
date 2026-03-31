using IssuePit.Notes.Core.Data;
using IssuePit.Notes.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Notes.Api.Services;

/// <summary>
/// Background service that runs nightly to compact old CRDT operation log entries.
/// Operations older than 30 days are grouped by day and merged into a single
/// compacted operation per day, reducing storage whilst preserving an auditable
/// daily-granularity history.
///
/// Compaction algorithm for each day bucket:
///  1. Fetch all non-compacted ops for that note on that day.
///  2. Compose their deltas sequentially using <see cref="OtEngine.Compose"/>.
///  3. Replace the individual ops with one <c>IsCompacted = true</c> row that carries
///     the composite delta and the highest SequenceNumber in the bucket.
///  4. Delete the original rows.
/// </summary>
public class NoteCompactionService(IServiceScopeFactory scopeFactory, ILogger<NoteCompactionService> logger)
    : BackgroundService
{
    // How old operations must be before they are eligible for compaction
    private static readonly TimeSpan CompactionThreshold = TimeSpan.FromDays(30);

    // Run once per day at 03:00 UTC to minimise impact during active editing hours
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay the first run until 03:00 UTC today (or tomorrow if it's past 03:00)
        var now = DateTime.UtcNow;
        var nextRun = now.Date.AddHours(3);
        if (nextRun <= now)
            nextRun = nextRun.AddDays(1);

        logger.LogInformation("[Compaction] Next compaction run scheduled for {NextRun:u}", nextRun);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = nextRun - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, stoppingToken);

            try
            {
                await RunCompactionAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "[Compaction] Compaction run failed");
            }

            nextRun = nextRun.AddDays(1);
        }
    }

    private async Task RunCompactionAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.Subtract(CompactionThreshold);
        logger.LogInformation("[Compaction] Running compaction for ops applied before {Cutoff:u}", cutoff);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();

        // Find all note IDs that have compactable ops
        var noteIds = await db.NoteOperations
            .Where(o => !o.IsCompacted && o.AppliedAt < cutoff)
            .Select(o => o.NoteId)
            .Distinct()
            .ToListAsync(ct);

        int totalCompacted = 0;
        foreach (var noteId in noteIds)
        {
            totalCompacted += await CompactNoteAsync(db, noteId, cutoff, ct);
        }

        logger.LogInformation("[Compaction] Compacted {Count} day-buckets across {Notes} notes",
            totalCompacted, noteIds.Count);
    }

    /// <summary>
    /// Compact all eligible operations for a single note.
    /// Returns the number of day-buckets compacted.
    /// </summary>
    private async Task<int> CompactNoteAsync(
        NotesDbContext db, Guid noteId, DateTime cutoff, CancellationToken ct)
    {
        var ops = await db.NoteOperations
            .Where(o => o.NoteId == noteId && !o.IsCompacted && o.AppliedAt < cutoff)
            .OrderBy(o => o.SequenceNumber)
            .ToListAsync(ct);

        if (ops.Count == 0) return 0;

        // Group by calendar day (UTC)
        var byDay = ops.GroupBy(o => o.AppliedAt.Date).OrderBy(g => g.Key);
        int bucketsCompacted = 0;

        foreach (var dayGroup in byDay)
        {
            var dayOps = dayGroup.OrderBy(o => o.SequenceNumber).ToList();
            if (dayOps.Count <= 1)
                continue; // Nothing to compact — a single op is already minimal

            // Compose all deltas for the day into a single delta
            var composed = OtEngine.Deserialize(dayOps[0].Delta);
            for (int i = 1; i < dayOps.Count; i++)
            {
                var next = OtEngine.Deserialize(dayOps[i].Delta);
                composed = OtEngine.Compose(composed, next);
            }

            // Keep the highest SequenceNumber as the compacted op's identifier
            var last = dayOps[^1];
            var compactedOp = new Core.Entities.NoteOperation
            {
                Id = Guid.NewGuid(),
                NoteId = noteId,
                ClientId = $"compacted:{dayGroup.Key:yyyy-MM-dd}",
                SequenceNumber = last.SequenceNumber,
                BaseSequence = dayOps[0].BaseSequence,
                Delta = OtEngine.Serialize(composed),
                AppliedAt = last.AppliedAt,
                IsCompacted = true,
            };

            // Remove all ops for this day and insert the compacted replacement
            db.NoteOperations.RemoveRange(dayOps);
            db.NoteOperations.Add(compactedOp);

            await db.SaveChangesAsync(ct);
            bucketsCompacted++;
        }

        return bucketsCompacted;
    }
}
