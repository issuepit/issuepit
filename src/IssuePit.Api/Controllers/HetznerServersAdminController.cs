using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

/// <summary>
/// Admin endpoints for managing Hetzner Cloud servers provisioned by IssuePit CI/CD.
/// Requires the caller to be an admin user.
/// </summary>
[ApiController]
[Route("api/admin/hetzner-servers")]
public class HetznerServersAdminController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    /// <summary>Lists all Hetzner servers for the current tenant (admin only).</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        if (ctx.CurrentUser is null || !ctx.CurrentUser.IsAdmin)
            return Forbid();

        var servers = await db.HetznerServers
            .Include(s => s.Organization)
            .Where(s => s.Organization.TenantId == ctx.CurrentTenant!.Id)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new HetznerServerDto(
                s.Id,
                s.HetznerServerId,
                s.OrgId,
                s.Organization.Name,
                s.Name,
                s.ServerType,
                s.Location,
                s.Ipv6Address,
                s.Ipv4Address,
                s.Status,
                s.CreatedAt,
                s.ReadyAt,
                s.LastJobEndedAt,
                s.DeletedAt,
                s.ActiveJobCount,
                s.TotalJobCount,
                s.CpuLoadPercent,
                s.RamUsedMb,
                s.RamTotalMb,
                s.SetupDurationSeconds,
                s.LastError))
            .ToListAsync();

        return Ok(servers);
    }

    /// <summary>Gets details of a single Hetzner server (admin only).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        if (ctx.CurrentUser is null || !ctx.CurrentUser.IsAdmin)
            return Forbid();

        var server = await db.HetznerServers
            .Include(s => s.Organization)
            .Where(s => s.Id == id && s.Organization.TenantId == ctx.CurrentTenant!.Id)
            .Select(s => new HetznerServerDto(
                s.Id,
                s.HetznerServerId,
                s.OrgId,
                s.Organization.Name,
                s.Name,
                s.ServerType,
                s.Location,
                s.Ipv6Address,
                s.Ipv4Address,
                s.Status,
                s.CreatedAt,
                s.ReadyAt,
                s.LastJobEndedAt,
                s.DeletedAt,
                s.ActiveJobCount,
                s.TotalJobCount,
                s.CpuLoadPercent,
                s.RamUsedMb,
                s.RamTotalMb,
                s.SetupDurationSeconds,
                s.LastError))
            .FirstOrDefaultAsync();

        if (server is null) return NotFound();
        return Ok(server);
    }

    /// <summary>
    /// Updates the status of a server to Deleted (admin reconcile action).
    /// Also writes a runtime history record so the cost dashboard is accurate.
    /// Call this when a server is known to have been deleted externally.
    /// </summary>
    [HttpPost("{id:guid}/mark-deleted")]
    public async Task<IActionResult> MarkDeleted(Guid id)
    {
        if (ctx.CurrentUser is null || !ctx.CurrentUser.IsAdmin)
            return Forbid();

        var server = await db.HetznerServers
            .Include(s => s.Organization)
            .FirstOrDefaultAsync(s => s.Id == id && s.Organization.TenantId == ctx.CurrentTenant!.Id);

        if (server is null) return NotFound();

        var now = DateTime.UtcNow;
        server.Status = HetznerServerStatus.Deleted;
        server.DeletedAt ??= now;

        // Write a runtime history record if one does not already exist for this server
        var historyExists = await db.HetznerServerRuntimeHistories
            .AnyAsync(h => h.HetznerServerId == server.Id);

        if (!historyExists)
        {
            var totalSeconds = (int)(now - server.CreatedAt).TotalSeconds;
            db.HetznerServerRuntimeHistories.Add(new HetznerServerRuntimeHistory
            {
                Id = Guid.NewGuid(),
                HetznerServerId = server.Id,
                OrgId = server.OrgId,
                ServerType = server.ServerType,
                Location = server.Location,
                ProvisionedAt = server.CreatedAt,
                ReadyAt = server.ReadyAt,
                DeletedAt = now,
                TotalRuntimeSeconds = totalSeconds,
                BillableSeconds = server.ReadyAt.HasValue
                    ? (int)(now - server.ReadyAt.Value).TotalSeconds
                    : totalSeconds,
                TotalJobCount = server.TotalJobCount,
                SetupDurationSeconds = server.SetupDurationSeconds,
                PeakCpuLoadPercent = server.CpuLoadPercent,
                PeakRamUsedMb = server.RamUsedMb,
                RecordedAt = now,
            });
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "Marked as deleted." });
    }

    /// <summary>
    /// Returns server runtime history for the current tenant, ordered by most recent first.
    /// Used to build a cost dashboard.
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        if (ctx.CurrentUser is null || !ctx.CurrentUser.IsAdmin)
            return Forbid();

        var history = await db.HetznerServerRuntimeHistories
            .Include(h => h.Organization)
            .Where(h => h.Organization.TenantId == ctx.CurrentTenant!.Id)
            .OrderByDescending(h => h.RecordedAt)
            .Select(h => new HetznerServerHistoryDto(
                h.Id,
                h.HetznerServerId,
                h.OrgId,
                h.Organization.Name,
                h.ServerType,
                h.Location,
                h.ProvisionedAt,
                h.ReadyAt,
                h.DeletedAt,
                h.TotalRuntimeSeconds,
                h.BillableSeconds,
                h.TotalJobCount,
                h.SetupDurationSeconds,
                h.EstimatedCostEuroCents,
                h.PeakCpuLoadPercent,
                h.PeakRamUsedMb,
                h.RecordedAt))
            .ToListAsync();

        return Ok(history);
    }
}

public record HetznerServerDto(
    Guid Id,
    long HetznerServerId,
    Guid OrgId,
    string OrgName,
    string Name,
    string ServerType,
    string Location,
    string? Ipv6Address,
    string? Ipv4Address,
    HetznerServerStatus Status,
    DateTime CreatedAt,
    DateTime? ReadyAt,
    DateTime? LastJobEndedAt,
    DateTime? DeletedAt,
    int ActiveJobCount,
    int TotalJobCount,
    double? CpuLoadPercent,
    int? RamUsedMb,
    int? RamTotalMb,
    int? SetupDurationSeconds,
    string? LastError);

public record HetznerServerHistoryDto(
    Guid Id,
    Guid HetznerServerId,
    Guid OrgId,
    string OrgName,
    string ServerType,
    string Location,
    DateTime ProvisionedAt,
    DateTime? ReadyAt,
    DateTime? DeletedAt,
    int? TotalRuntimeSeconds,
    int? BillableSeconds,
    int TotalJobCount,
    int? SetupDurationSeconds,
    int? EstimatedCostEuroCents,
    double? PeakCpuLoadPercent,
    int? PeakRamUsedMb,
    DateTime RecordedAt);

