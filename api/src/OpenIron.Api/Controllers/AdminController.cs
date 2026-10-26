using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Auth;
using OpenIron.Api.Contracts;
using OpenIron.Api.Data;
using OpenIron.Api.Data.Entities;
using OpenIron.Api.Services;

namespace OpenIron.Api.Controllers;

/// <summary>
/// Server administration. Admins see who has an account and how active it is, but never anyone's workout
/// contents or body weight. Admins can't act on their own account here, which also means a server can't be
/// left without an admin: whoever is acting is always one.
/// </summary>
[ApiController]
[Authorize(Roles = SessionValidator.AdminRole)]
[Route("api/admin")]
public class AdminController(AppDbContext db, IPasswordHasher<User> hasher, AccountService accounts, InviteService invites)
    : ControllerBase
{
    [HttpGet("overview")]
    public async Task<AdminOverviewDto> Overview()
    {
        var since = DateTime.UtcNow.AddDays(-30);
        return new AdminOverviewDto(
            await db.Users.CountAsync(),
            await db.Users.CountAsync(u => u.IsAdmin),
            await db.Users.CountAsync(u => u.IsDisabled),
            await db.Workouts.CountAsync(),
            await db.Workouts.CountAsync(w => w.PerformedAt >= since),
            (await db.ServerSettings.AsNoTracking().SingleAsync()).RegistrationOpen);
    }

    [HttpGet("users")]
    public async Task<List<AdminUserDto>> Users()
    {
        var me = User.GetUserId();
        var users = await db.Users.AsNoTracking().OrderBy(u => u.NormalizedUsername).ToListAsync();
        var activity = (await db.Workouts.AsNoTracking()
                .GroupBy(w => w.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count(), Last = g.Max(w => w.PerformedAt) })
                .ToListAsync())
            .ToDictionary(a => a.UserId);

        return users.Select(u =>
        {
            activity.TryGetValue(u.Id, out var a);
            return new AdminUserDto(u.Id, u.Username, u.CreatedAt, u.IsAdmin, u.IsDisabled, u.ShareActivity,
                u.Id == me, a?.Count ?? 0, a?.Last);
        }).ToList();
    }

    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult<AdminUserDto>> UpdateUser(Guid id, AdminUserUpdate update)
    {
        if (update.IsAdmin is null && update.IsDisabled is null)
            return BadRequest(new ProblemDetails { Title = "Nothing to update." });
        if (id == User.GetUserId()) return OwnAccount();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        if (update.IsAdmin is { } isAdmin) user.IsAdmin = isAdmin;
        if (update.IsDisabled is { } isDisabled)
        {
            // Disabling must end the user's sessions for good. Rotating the stamp means tokens they held before
            // can't come back to life when the account is enabled again.
            if (isDisabled != user.IsDisabled) user.SecurityStamp = Guid.NewGuid();
            user.IsDisabled = isDisabled;
        }
        await db.SaveChangesAsync();

        return (await Users()).First(u => u.Id == id);
    }

    [HttpPost("users/{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, AdminResetPasswordRequest request)
    {
        if (id == User.GetUserId()) return OwnAccount();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        user.SecurityStamp = Guid.NewGuid();
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        if (id == User.GetUserId()) return OwnAccount();
        if (!await db.Users.AnyAsync(u => u.Id == id)) return NotFound();

        await accounts.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("settings")]
    public async Task<AdminSettingsDto> GetSettings() =>
        new((await db.ServerSettings.AsNoTracking().SingleAsync()).RegistrationOpen);

    [HttpPut("settings")]
    public async Task<AdminSettingsDto> UpdateSettings(AdminSettingsDto request)
    {
        var settings = await db.ServerSettings.SingleAsync();
        settings.RegistrationOpen = request.RegistrationOpen;
        await db.SaveChangesAsync();
        return new AdminSettingsDto(settings.RegistrationOpen);
    }

    private ObjectResult OwnAccount() =>
        Conflict(new ProblemDetails { Title = "You can't do that to your own account. Use Settings, or ask another admin." });

    [HttpGet("invites")]
    public async Task<List<InviteDto>> Invites()
    {
        var now = DateTime.UtcNow;
        var rows = await db.Invites.AsNoTracking().OrderByDescending(i => i.CreatedAt).Take(200).ToListAsync();

        var userIds = rows.Where(i => i.UsedByUserId != null).Select(i => i.UsedByUserId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);

        return rows.Select(i => ToDto(i, now, i.UsedByUserId is { } id && names.TryGetValue(id, out var name) ? name : null)).ToList();
    }

    /// <summary>Creates a single-use invite. The code is in this response and nowhere else, so the admin has to pass it on now.</summary>
    [HttpPost("invites")]
    public async Task<ActionResult<CreatedInviteDto>> CreateInvite(CreateInviteRequest request)
    {
        var now = DateTime.UtcNow;
        var code = InviteService.NewCode();
        var invite = new Invite
        {
            CodeHash = InviteService.Hash(code),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = now,
            CreatedByUserId = User.GetUserId(),
            ExpiresAt = request.ExpiresInDays is { } days ? now.AddDays(days) : null,
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync();

        return new CreatedInviteDto(ToDto(invite, now, null), code);
    }

    /// <summary>Revokes an invite that hasn't been used. Revoking one that is already revoked is a no-op.</summary>
    [HttpDelete("invites/{id:guid}")]
    public async Task<IActionResult> RevokeInvite(Guid id)
    {
        var invite = await db.Invites.FirstOrDefaultAsync(i => i.Id == id);
        if (invite is null) return NotFound();
        if (invite.UsedAt is not null)
            return Conflict(new ProblemDetails { Title = "That invite has already been used." });

        invite.RevokedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static InviteDto ToDto(Invite i, DateTime now, string? usedBy) => new(
        i.Id, i.Note, i.CreatedAt, i.ExpiresAt, i.StatusAt(now).ToString().ToLowerInvariant(), usedBy, i.UsedAt);
}
