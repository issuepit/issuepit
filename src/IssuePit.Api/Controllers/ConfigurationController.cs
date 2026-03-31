using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigurationController(IssuePitDbContext db, TenantContext tenant) : ControllerBase
{
    // --- Helpers ---

    /// <summary>
    /// Returns the requested orgId when provided, or resolves to the first organization
    /// found for the current tenant. Returns <see cref="Guid.Empty"/> when no org exists.
    /// </summary>
    private async Task<Guid> ResolveOrgIdAsync(Guid? requestedOrgId) =>
        requestedOrgId ?? await db.Organizations
            .Where(o => o.TenantId == tenant.CurrentTenant!.Id)
            .Select(o => o.Id)
            .FirstOrDefaultAsync();

    // --- API Keys ---

    [HttpGet("keys")]
    public async Task<IActionResult> GetKeys()
    {
        var keys = await db.ApiKeys
            .Where(k => k.Organization.TenantId == tenant.CurrentTenant!.Id)
            .Select(k => new
            {
                k.Id,
                k.OrgId,
                k.ProjectId,
                k.TeamId,
                k.UserId,
                k.Name,
                k.Provider,
                ProviderName = k.Provider.ToString(),
                k.JiraBaseUrl,
                k.JiraEmail,
                k.CreatedAt,
                k.ExpiresAt,
                // Never return the encrypted value
            })
            .ToListAsync();
        return Ok(keys);
    }

    [HttpPost("keys")]
    public async Task<IActionResult> CreateKey([FromBody] ApiKeyRequest req)
    {
        var orgId = await ResolveOrgIdAsync(req.OrgId);

        if (orgId == Guid.Empty)
            return BadRequest("No organization found for current tenant.");

        var key = new ApiKey
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            ProjectId = req.ProjectId,
            TeamId = req.TeamId,
            UserId = req.UserId,
            Name = req.Name,
            Provider = req.Provider,
            // In production, encrypt before storing. Placeholder prefix marks it as unencrypted for now.
            EncryptedValue = $"plain:{req.Value}",
            ExpiresAt = req.ExpiresAt,
            JiraBaseUrl = string.IsNullOrWhiteSpace(req.JiraBaseUrl) ? null : req.JiraBaseUrl.Trim().TrimEnd('/'),
            JiraEmail = string.IsNullOrWhiteSpace(req.JiraEmail) ? null : req.JiraEmail.Trim(),
        };
        db.ApiKeys.Add(key);
        await db.SaveChangesAsync();
        return Created($"/api/config/keys/{key.Id}", new { key.Id, key.Name, key.Provider, key.ProjectId, key.TeamId, key.UserId, key.JiraBaseUrl, key.JiraEmail, key.CreatedAt });
    }

    [HttpDelete("keys/{id:guid}")]
    public async Task<IActionResult> DeleteKey(Guid id)
    {
        var key = await db.ApiKeys.FindAsync(id);
        if (key is null) return NotFound();
        db.ApiKeys.Remove(key);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Runtime Configurations ---

    [HttpGet("runtimes")]
    public async Task<IActionResult> GetRuntimes()
    {
        var runtimes = await db.RuntimeConfigurations
            .Where(r => r.Organization.TenantId == tenant.CurrentTenant!.Id)
            .Select(r => new
            {
                r.Id,
                r.OrgId,
                r.Name,
                r.Type,
                TypeName = r.Type.ToString(),
                r.Configuration,
                r.IsDefault,
                r.MaxConcurrentAgents,
                r.CreatedAt,
            })
            .ToListAsync();
        return Ok(runtimes);
    }

    [HttpPost("runtimes")]
    public async Task<IActionResult> CreateRuntime([FromBody] RuntimeConfigRequest req)
    {
        var orgId = await ResolveOrgIdAsync(req.OrgId);

        if (orgId == Guid.Empty)
            return BadRequest("No organization found for current tenant.");

        if (req.IsDefault)
        {
            // Clear existing default for this org
            var existing = await db.RuntimeConfigurations
                .Where(r => r.OrgId == orgId && r.IsDefault)
                .ToListAsync();
            existing.ForEach(r => r.IsDefault = false);
        }

        var runtime = new RuntimeConfiguration
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = req.Name,
            Type = req.Type,
            Configuration = req.Configuration,
            IsDefault = req.IsDefault,
            MaxConcurrentAgents = req.MaxConcurrentAgents,
        };
        db.RuntimeConfigurations.Add(runtime);
        await db.SaveChangesAsync();
        return Created($"/api/config/runtimes/{runtime.Id}", runtime);
    }

    [HttpPut("runtimes/{id:guid}")]
    public async Task<IActionResult> UpdateRuntime(Guid id, [FromBody] RuntimeConfigRequest req)
    {
        var runtime = await db.RuntimeConfigurations.FindAsync(id);
        if (runtime is null) return NotFound();

        if (req.IsDefault && !runtime.IsDefault)
        {
            var existing = await db.RuntimeConfigurations
                .Where(r => r.OrgId == runtime.OrgId && r.IsDefault && r.Id != id)
                .ToListAsync();
            existing.ForEach(r => r.IsDefault = false);
        }

        runtime.Name = req.Name;
        runtime.Type = req.Type;
        runtime.Configuration = req.Configuration;
        runtime.IsDefault = req.IsDefault;
        runtime.MaxConcurrentAgents = req.MaxConcurrentAgents;
        await db.SaveChangesAsync();
        return Ok(runtime);
    }

    [HttpDelete("runtimes/{id:guid}")]
    public async Task<IActionResult> DeleteRuntime(Guid id)
    {
        var runtime = await db.RuntimeConfigurations.FindAsync(id);
        if (runtime is null) return NotFound();
        db.RuntimeConfigurations.Remove(runtime);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Returns the current pool status: how many agents/CI-CD runs are active or pending,
    /// and the configured limit for each runtime and organization.
    /// </summary>
    [HttpGet("pool-status")]
    public async Task<IActionResult> GetPoolStatus()
    {
        if (tenant.CurrentTenant is null) return Unauthorized();

        // Agent sessions per runtime configuration
        var agentSessions = await db.AgentSessions
            .Where(s => s.Issue.Project!.Organization.TenantId == tenant.CurrentTenant.Id
                        && (s.Status == AgentSessionStatus.Running || s.Status == AgentSessionStatus.Pending))
            .GroupBy(s => s.RuntimeConfigId)
            .Select(g => new
            {
                RuntimeConfigId = g.Key,
                Running = g.Count(s => s.Status == AgentSessionStatus.Running),
                Pending = g.Count(s => s.Status == AgentSessionStatus.Pending),
            })
            .ToListAsync();

        var runtimeConfigs = await db.RuntimeConfigurations
            .Where(r => r.Organization.TenantId == tenant.CurrentTenant.Id)
            .Select(r => new { r.Id, r.Name, r.MaxConcurrentAgents })
            .ToListAsync();

        var agentPools = runtimeConfigs.Select(rc =>
        {
            var stats = agentSessions.FirstOrDefault(s => s.RuntimeConfigId == rc.Id);
            return new
            {
                runtimeConfigId = (Guid?)rc.Id,
                runtimeName = rc.Name,
                maxConcurrentAgents = rc.MaxConcurrentAgents,
                runningAgents = stats?.Running ?? 0,
                pendingAgents = stats?.Pending ?? 0,
            };
        }).ToList();

        // Also include unbound sessions (no runtime config = local Docker default)
        var unboundStats = agentSessions.FirstOrDefault(s => s.RuntimeConfigId == null);
        if (unboundStats is not null || !agentPools.Any())
        {
            agentPools.Insert(0, new
            {
                runtimeConfigId = (Guid?)null,
                runtimeName = "Default (Docker)",
                maxConcurrentAgents = 0,
                runningAgents = unboundStats?.Running ?? 0,
                pendingAgents = unboundStats?.Pending ?? 0,
            });
        }

        // CI/CD runs per organization
        var orgs = await db.Organizations
            .Where(o => o.TenantId == tenant.CurrentTenant.Id)
            .Select(o => new { o.Id, o.Name, o.MaxConcurrentRunners })
            .ToListAsync();

        var cicdRunsActive = await db.CiCdRuns
            .Where(r => r.Project.Organization.TenantId == tenant.CurrentTenant.Id
                        && (r.Status == CiCdRunStatus.Running || r.Status == CiCdRunStatus.Pending))
            .GroupBy(r => r.Project.OrgId)
            .Select(g => new
            {
                OrgId = g.Key,
                Running = g.Count(r => r.Status == CiCdRunStatus.Running),
                Pending = g.Count(r => r.Status == CiCdRunStatus.Pending),
            })
            .ToListAsync();

        var cicdPools = orgs.Select(o =>
        {
            var stats = cicdRunsActive.FirstOrDefault(r => r.OrgId == o.Id);
            return new
            {
                orgId = o.Id,
                orgName = o.Name,
                maxConcurrentRunners = o.MaxConcurrentRunners,
                runningCiCdRuns = stats?.Running ?? 0,
                pendingCiCdRuns = stats?.Pending ?? 0,
            };
        }).ToList();

        return Ok(new { agentPools, cicdPools });
    }

    // --- Telegram Bots ---

    [HttpGet("telegram-bots")]
    public async Task<IActionResult> GetTelegramBots()
    {
        var bots = await db.TelegramBots
            .Where(b =>
                (b.OrgId != null && db.Organizations.Any(o => o.Id == b.OrgId && o.TenantId == tenant.CurrentTenant!.Id)) ||
                (b.ProjectId != null && db.Projects.Any(p => p.Id == b.ProjectId && p.Organization.TenantId == tenant.CurrentTenant!.Id)))
            .Select(b => new TelegramBotResponse(
                b.Id, b.Name, b.OrgId, b.ProjectId, b.ChatId,
                b.Events, b.IsSilent, b.DigestInterval,
                b.RateLimitCount, b.RateLimitWindowMinutes, b.SilentMode,
                b.CreatedAt))
            .ToListAsync();
        return Ok(bots);
    }

    [HttpPost("telegram-bots")]
    public async Task<IActionResult> CreateTelegramBot([FromBody] TelegramBotRequest req)
    {
        var bot = new TelegramBot
        {
            Id = Guid.NewGuid(),
            OrgId = req.OrgId,
            ProjectId = req.ProjectId,
            Name = req.Name,
            // In production, encrypt before storing. Placeholder prefix marks it as unencrypted for now.
            EncryptedBotToken = $"plain:{req.BotToken}",
            ChatId = req.ChatId,
            Events = req.Events,
            IsSilent = req.IsSilent,
            DigestInterval = req.DigestInterval,
            RateLimitCount = req.RateLimitCount,
            RateLimitWindowMinutes = req.RateLimitWindowMinutes,
            SilentMode = req.SilentMode,
        };
        db.TelegramBots.Add(bot);
        await db.SaveChangesAsync();
        return Created($"/api/config/telegram-bots/{bot.Id}", new TelegramBotResponse(bot.Id, bot.Name, bot.OrgId, bot.ProjectId, bot.ChatId, bot.Events, bot.IsSilent, bot.DigestInterval, bot.RateLimitCount, bot.RateLimitWindowMinutes, bot.SilentMode, bot.CreatedAt));
    }

    [HttpPut("telegram-bots/{id:guid}")]
    public async Task<IActionResult> UpdateTelegramBot(Guid id, [FromBody] TelegramBotRequest req)
    {
        var bot = await db.TelegramBots
            .Where(b =>
                (b.OrgId != null && db.Organizations.Any(o => o.Id == b.OrgId && o.TenantId == tenant.CurrentTenant!.Id)) ||
                (b.ProjectId != null && db.Projects.Any(p => p.Id == b.ProjectId && p.Organization.TenantId == tenant.CurrentTenant!.Id)))
            .FirstOrDefaultAsync(b => b.Id == id);
        if (bot is null) return NotFound();

        bot.Name = req.Name;
        bot.OrgId = req.OrgId;
        bot.ProjectId = req.ProjectId;
        bot.ChatId = req.ChatId;
        bot.Events = req.Events;
        bot.IsSilent = req.IsSilent;
        bot.DigestInterval = req.DigestInterval;
        bot.RateLimitCount = req.RateLimitCount;
        bot.RateLimitWindowMinutes = req.RateLimitWindowMinutes;
        bot.SilentMode = req.SilentMode;
        if (!string.IsNullOrEmpty(req.BotToken))
            bot.EncryptedBotToken = $"plain:{req.BotToken}";
        await db.SaveChangesAsync();
        return Ok(new TelegramBotResponse(bot.Id, bot.Name, bot.OrgId, bot.ProjectId, bot.ChatId, bot.Events, bot.IsSilent, bot.DigestInterval, bot.RateLimitCount, bot.RateLimitWindowMinutes, bot.SilentMode, bot.CreatedAt));
    }

    [HttpDelete("telegram-bots/{id:guid}")]
    public async Task<IActionResult> DeleteTelegramBot(Guid id)
    {
        var bot = await db.TelegramBots
            .Where(b =>
                (b.OrgId != null && db.Organizations.Any(o => o.Id == b.OrgId && o.TenantId == tenant.CurrentTenant!.Id)) ||
                (b.ProjectId != null && db.Projects.Any(p => p.Id == b.ProjectId && p.Organization.TenantId == tenant.CurrentTenant!.Id)))
            .FirstOrDefaultAsync(b => b.Id == id);
        if (bot is null) return NotFound();
        db.TelegramBots.Remove(bot);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record ApiKeyRequest(Guid? OrgId, string Name, ApiKeyProvider Provider, string Value, DateTime? ExpiresAt, Guid? ProjectId = null, Guid? TeamId = null, Guid? UserId = null, string? JiraBaseUrl = null, string? JiraEmail = null);
public record RuntimeConfigRequest(Guid? OrgId, string Name, RuntimeType Type, string Configuration, bool IsDefault, int MaxConcurrentAgents = 0);
public record TelegramBotRequest(string Name, string BotToken, string ChatId, int Events, bool IsSilent, DigestInterval DigestInterval, Guid? OrgId, Guid? ProjectId, int RateLimitCount = 0, int RateLimitWindowMinutes = 0, TelegramSilentMode SilentMode = TelegramSilentMode.None);
public record TelegramBotResponse(Guid Id, string Name, Guid? OrgId, Guid? ProjectId, string ChatId, int Events, bool IsSilent, DigestInterval DigestInterval, int RateLimitCount, int RateLimitWindowMinutes, TelegramSilentMode SilentMode, DateTime CreatedAt);
