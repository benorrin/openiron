using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Data;

namespace OpenIron.Api.Auth;

/// <summary>
/// Checks a signature-valid token against the database on every request, so disabling a user, resetting their
/// password or revoking admin takes effect immediately rather than when their token expires.
/// </summary>
public static class SessionValidator
{
    public const string AdminRole = "Admin";

    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal!;
        var subject = principal.FindFirstValue(TokenService.SubjectClaim);
        var stamp = principal.FindFirstValue(TokenService.StampClaim);

        if (!Guid.TryParse(subject, out var userId) || stamp is null)
        {
            context.Fail("Token is missing required claims.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsAdmin, u.IsDisabled, u.SecurityStamp })
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (user is null || user.IsDisabled || !string.Equals(stamp, user.SecurityStamp.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            context.Fail("Session is no longer valid.");
            return;
        }

        // The role comes from the database rather than the token, so it is never stale.
        if (user.IsAdmin) ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(ClaimTypes.Role, AdminRole));
    }
}
