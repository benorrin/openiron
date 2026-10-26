using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Auth;
using OpenIron.Api.Contracts;
using OpenIron.Api.Data;
using OpenIron.Api.Data.Entities;

namespace OpenIron.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokens, IPasswordHasher<User> hasher) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var normalized = request.Username.ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.NormalizedUsername == normalized))
            return Conflict(new ProblemDetails { Title = "That username is already taken." });

        var user = new User { Username = request.Username, NormalizedUsername = normalized, PasswordHash = "" };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent registration of the same name; anything else is a real failure.
            db.ChangeTracker.Clear();
            if (!await db.Users.AnyAsync(u => u.NormalizedUsername == normalized)) throw;
            return Conflict(new ProblemDetails { Title = "That username is already taken." });
        }

        return Ok(Issue(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedUsername == request.Username.ToLowerInvariant());
        var result = user is null
            ? PasswordVerificationResult.Failed
            : hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (user is null || result == PasswordVerificationResult.Failed)
            return Unauthorized(new ProblemDetails { Title = "Invalid username or password." });

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync();
        }

        return Ok(Issue(user));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await db.Users.FindAsync(User.GetUserId());
        return user is null ? Unauthorized() : new UserDto(user.Id, user.Username);
    }

    private AuthResponse Issue(User user)
    {
        var (token, expires) = tokens.Create(user);
        return new AuthResponse(token, expires, new UserDto(user.Id, user.Username));
    }
}
