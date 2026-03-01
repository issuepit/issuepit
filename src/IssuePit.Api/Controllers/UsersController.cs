using BCrypt.Net;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
public class UsersController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        if (ctx.CurrentUser is null || !ctx.CurrentUser.IsAdmin)
            return Forbid();

        var users = await db.Users
            .Where(u => u.TenantId == ctx.CurrentUser.TenantId)
            .OrderBy(u => u.Username)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                u.IsAdmin,
                u.CreatedAt,
                HasPassword = u.PasswordHash != null,
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req)
    {
        if (ctx.CurrentUser is null || !ctx.CurrentUser.IsAdmin)
            return Forbid();

        if (string.IsNullOrWhiteSpace(req.Username))
            return BadRequest("Username is required.");

        var exists = await db.Users.AnyAsync(
            u => u.Username == req.Username && u.TenantId == ctx.CurrentUser.TenantId);
        if (exists)
            return Conflict("Username already taken.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = ctx.CurrentUser.TenantId,
            Username = req.Username,
            Email = req.Email ?? $"{req.Username}@localhost",
            IsAdmin = req.IsAdmin,
            CreatedAt = DateTime.UtcNow,
        };

        if (!string.IsNullOrEmpty(req.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Created($"/api/admin/users/{user.Id}", new
        {
            user.Id,
            user.Username,
            user.Email,
            user.IsAdmin,
            user.CreatedAt,
            HasPassword = user.PasswordHash != null,
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest req)
    {
        if (ctx.CurrentUser is null || !ctx.CurrentUser.IsAdmin)
            return Forbid();

        var user = await db.Users.FindAsync(id);
        if (user is null || user.TenantId != ctx.CurrentUser.TenantId)
            return NotFound();

        if (!string.IsNullOrWhiteSpace(req.Username) && req.Username != user.Username)
        {
            var taken = await db.Users.AnyAsync(
                u => u.Username == req.Username && u.TenantId == ctx.CurrentUser.TenantId && u.Id != id);
            if (taken)
                return Conflict("Username already taken.");
            user.Username = req.Username;
        }
        if (!string.IsNullOrWhiteSpace(req.Email))
            user.Email = req.Email;
        if (req.IsAdmin.HasValue)
            user.IsAdmin = req.IsAdmin.Value;
        if (!string.IsNullOrEmpty(req.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

        await db.SaveChangesAsync();

        return Ok(new
        {
            user.Id,
            user.Username,
            user.Email,
            user.IsAdmin,
            user.CreatedAt,
            HasPassword = user.PasswordHash != null,
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        if (ctx.CurrentUser is null || !ctx.CurrentUser.IsAdmin)
            return Forbid();

        var user = await db.Users.FindAsync(id);
        if (user is null || user.TenantId != ctx.CurrentUser.TenantId)
            return NotFound();

        // Prevent deleting self.
        if (user.Id == ctx.CurrentUser.Id)
            return BadRequest("Cannot delete your own account.");

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateUserRequest(string Username, string? Email, string? Password, bool IsAdmin = false);
public record UpdateUserRequest(string? Username, string? Email, string? Password, bool? IsAdmin);
