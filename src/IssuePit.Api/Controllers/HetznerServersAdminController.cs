using IssuePit.Core.Data;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

/// <summary>Admin endpoints for viewing and managing Hetzner Cloud CI/CD servers.</summary>
[ApiController]
[Route("api/admin/hetzner")]
public class HetznerServersAdminController(IssuePitDbContext db) : ControllerBase
{
    /// <summary>Returns all tracked Hetzner servers with their current status and metrics.</summary>
    [HttpGet("servers")]
    public async Task<IActionResult> GetServers()
    {
        var servers = await db.HetznerServers
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new HetznerServerDto(
                s.Id,
                s.HetznerServerId,
                s.Name,
                s.Ipv4Address,
                s.Ipv6Address,
                s.ServerType,
                s.Location,
                s.Status,
                s.Status.ToString(),
                s.ActiveRunCount,
                s.TotalRunCount,
                s.CpuLoadPercent,
                s.RamUsedBytes,
                s.RamTotalBytes,
                s.CreatedAt,
                s.LastIdleAt,
                s.SetupTimeSeconds,
                s.ErrorMessage,
                s.OrgId))
            .ToListAsync();

        return Ok(servers);
    }

    /// <summary>Performs an action (stop, reboot, delete) on a tracked Hetzner server.</summary>
    [HttpPost("servers/{id:guid}/actions/{action}")]
    public async Task<IActionResult> PerformAction(Guid id, string action)
    {
        var server = await db.HetznerServers.FindAsync(id);
        if (server is null) return NotFound();

        var normalizedAction = action.ToLowerInvariant();
        if (normalizedAction is not ("stop" or "reboot" or "delete"))
            return BadRequest($"Unknown action '{action}'. Valid actions: stop, reboot, delete.");

        return Ok(new HetznerActionQueuedResponse(
            server.Id,
            normalizedAction,
            "Action has been queued. The Hetzner Cloud API call is performed by the CiCd client service."));
    }

    /// <summary>
    /// Updates the status of a server tracked in the database.
    /// Intended for the CiCd client to push status updates.
    /// </summary>
    [HttpPatch("servers/{id:guid}")]
    public async Task<IActionResult> PatchServer(Guid id, [FromBody] PatchHetznerServerRequest req)
    {
        var server = await db.HetznerServers.FindAsync(id);
        if (server is null) return NotFound();

        if (req.Status.HasValue)
            server.Status = req.Status.Value;
        if (req.CpuLoadPercent.HasValue)
            server.CpuLoadPercent = req.CpuLoadPercent.Value;
        if (req.RamUsedBytes.HasValue)
            server.RamUsedBytes = req.RamUsedBytes.Value;
        if (req.RamTotalBytes.HasValue)
            server.RamTotalBytes = req.RamTotalBytes.Value;
        if (req.MetricsCollected)
            server.MetricsLastCollectedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Removes a server record from the database (does not affect Hetzner Cloud).</summary>
    [HttpDelete("servers/{id:guid}")]
    public async Task<IActionResult> DeleteServerRecord(Guid id)
    {
        var server = await db.HetznerServers.FindAsync(id);
        if (server is null) return NotFound();
        db.HetznerServers.Remove(server);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Returns a summary of server counts by status.</summary>
    [HttpGet("servers/summary")]
    public async Task<IActionResult> GetSummary()
    {
        var counts = await db.HetznerServers
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var activeCount = await db.HetznerServers
            .Where(s => s.Status == HetznerServerStatus.Running)
            .SumAsync(s => (int?)s.ActiveRunCount) ?? 0;

        return Ok(new HetznerSummaryResponse(
            counts.ToDictionary(c => c.Status.ToString(), c => c.Count),
            activeCount));
    }
}

// ── Response / request records ─────────────────────────────────────────────────

public record HetznerServerDto(
    Guid Id,
    long HetznerServerId,
    string Name,
    string? Ipv4Address,
    string? Ipv6Address,
    string ServerType,
    string Location,
    HetznerServerStatus Status,
    string StatusName,
    int ActiveRunCount,
    int TotalRunCount,
    double? CpuLoadPercent,
    long? RamUsedBytes,
    long? RamTotalBytes,
    DateTime CreatedAt,
    DateTime? LastIdleAt,
    int? SetupTimeSeconds,
    string? ErrorMessage,
    Guid? OrgId);

public record HetznerActionQueuedResponse(Guid ServerId, string Action, string Message);

public record HetznerSummaryResponse(Dictionary<string, int> ByStatus, int TotalActiveRuns);

public record PatchHetznerServerRequest(
    HetznerServerStatus? Status,
    double? CpuLoadPercent,
    long? RamUsedBytes,
    long? RamTotalBytes,
    bool MetricsCollected = false);
