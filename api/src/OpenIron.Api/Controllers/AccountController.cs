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
/// Actions a user takes on their own account. A wrong password is a 400, not a 401: the front end treats 401
/// as "your session has ended" and would sign the user out for a typo.
/// </summary>
[ApiController]
[Authorize]
[Route("api/account")]
public class AccountController(
    AppDbContext db,
    TokenService tokens,
    IPasswordHasher<User> hasher,
    AccountService accounts) : ControllerBase
{
    [HttpPut("password")]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == User.GetUserId());
        if (user is null) return Unauthorized();

        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest(new ProblemDetails { Title = "Your current password is incorrect." });

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        user.SecurityStamp = Guid.NewGuid(); // signs out every other session
        await db.SaveChangesAsync();

        // The caller's own token is now stale too, so hand back a fresh one to keep them signed in.
        var (token, expires) = tokens.Create(user);
        return new AuthResponse(token, expires, new UserDto(user.Id, user.Username, user.IsAdmin));
    }

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(DeleteAccountRequest request)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == User.GetUserId());
        if (user is null) return Unauthorized();

        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return BadRequest(new ProblemDetails { Title = "That password is incorrect." });

        if (user.IsAdmin && !await accounts.HasAnotherAdminAsync(user.Id))
            return Conflict(new ProblemDetails { Title = "You are the only admin. Make someone else an admin before deleting your account." });

        await accounts.DeleteAsync(user.Id);
        return NoContent();
    }
}
