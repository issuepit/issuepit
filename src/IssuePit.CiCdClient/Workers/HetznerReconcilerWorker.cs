using IssuePit.CiCdClient.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.CiCdClient.Workers;

/// <summary>
/// Background service that periodically checks for Hetzner servers in the
/// <see cref="HetznerServerStatus.Draining"/> state and deletes them once the
/// spin-down cooldown has elapsed.
///
/// Also handles orphan cleanup: servers that are still tracked in the DB but have
/// been deleted externally in the Hetzner Cloud console will be marked as
/// <see cref="HetznerServerStatus.Deleted"/> so the UI stays accurate.
/// </summary>
public class HetznerReconcilerWorker(
    ILogger<HetznerReconcilerWorker> logger,
    IConfiguration configuration,
    IServiceProvider services,
    HetznerCloudService hetznerCloud) : BackgroundService
{
    private const int DefaultSpinDownCooldownMinutes = 10;
    private const int ReconcileIntervalSeconds = 60;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("HetznerReconcilerWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error during Hetzner server reconciliation");
            }

            await Task.Delay(TimeSpan.FromSeconds(ReconcileIntervalSeconds), stoppingToken);
        }
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        var cooldown = int.TryParse(configuration["Hetzner:SpinDownCooldownMinutes"], out var c)
            ? c
            : DefaultSpinDownCooldownMinutes;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        // Find servers that have been idle longer than the cooldown.
        var cutoff = DateTime.UtcNow.AddMinutes(-cooldown);
        var toDelete = await db.HetznerServers
            .Where(s => s.Status == HetznerServerStatus.Draining
                        && s.LastIdleAt.HasValue
                        && s.LastIdleAt.Value < cutoff)
            .ToListAsync(ct);

        foreach (var server in toDelete)
        {
            logger.LogInformation(
                "Deleting Hetzner server '{Name}' (id={HetznerServerId}) — idle since {IdleSince}",
                server.Name, server.HetznerServerId, server.LastIdleAt);

            try
            {
                var apiToken = await ResolveApiTokenAsync(db, server, ct);
                if (!string.IsNullOrWhiteSpace(apiToken))
                    await hetznerCloud.DeleteServerAsync(apiToken, server.HetznerServerId, ct);

                server.Status = HetznerServerStatus.Deleted;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete Hetzner server {Name}", server.Name);
                server.ErrorMessage = ex.Message;
                server.Status = HetznerServerStatus.Error;
                await db.SaveChangesAsync(ct);
            }
        }

        // Mark servers stuck in Provisioning/Initializing for too long as errors.
        var stuckCutoff = DateTime.UtcNow.AddMinutes(-30);
        var stuck = await db.HetznerServers
            .Where(s => (s.Status == HetznerServerStatus.Provisioning || s.Status == HetznerServerStatus.Initializing)
                        && s.CreatedAt < stuckCutoff)
            .ToListAsync(ct);

        foreach (var server in stuck)
        {
            logger.LogWarning(
                "Hetzner server '{Name}' has been in {Status} for >30 min — marking as Error",
                server.Name, server.Status);
            server.Status = HetznerServerStatus.Error;
            server.ErrorMessage = $"Timed out in {server.Status} state.";
        }

        if (stuck.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private async Task<string?> ResolveApiTokenAsync(IssuePitDbContext db, HetznerServer server, CancellationToken ct)
    {
        if (server.OrgId.HasValue)
        {
            var key = await db.ApiKeys.FirstOrDefaultAsync(
                k => k.OrgId == server.OrgId.Value
                     && k.ProjectId == null
                     && k.Provider == ApiKeyProvider.Hetzner, ct);

            if (key is not null)
                return DecryptApiKeyValue(key.EncryptedValue);
        }

        return configuration["Hetzner:ApiToken"];
    }

    private static string DecryptApiKeyValue(string encryptedValue) =>
        encryptedValue.StartsWith("plain:", StringComparison.Ordinal)
            ? encryptedValue["plain:".Length..]
            : encryptedValue;
}
