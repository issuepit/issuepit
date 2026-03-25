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

        server.Status = HetznerServerStatus.Deleted;
        server.DeletedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { message = "Marked as deleted." });
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
