using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Data;
using OpenIron.Api.Data.Entities;

namespace OpenIron.Api.Auth;

public static class AdminSeeder
{
    /// <summary>
    /// Makes the configured account an admin, creating it if needed. An existing account keeps its password:
    /// the configured one is only used when the account doesn't exist yet.
    /// </summary>
    public static async Task EnsureAsync(AppDbContext db, IPasswordHasher<User> hasher, AdminOptions options, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(options.Username)) return;

        var username = options.Username.Trim();
        var normalized = username.ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedUsername == normalized);

        if (user is null)
        {
            if (string.IsNullOrEmpty(options.Password) || options.Password.Length < 8)
                throw new InvalidOperationException("Admin:Password must be at least 8 characters when Admin:Username names a new account.");

            user = new User { Username = username, NormalizedUsername = normalized, PasswordHash = "", IsAdmin = true };
            user.PasswordHash = hasher.HashPassword(user, options.Password);
            db.Users.Add(user);
            logger.LogInformation("Created admin account {Username}", username);
        }
        else if (!user.IsAdmin || user.IsDisabled)
        {
            user.IsAdmin = true;
            // Re-enabling must not revive tokens issued before the account was disabled.
            if (user.IsDisabled) user.SecurityStamp = Guid.NewGuid();
            user.IsDisabled = false;
            logger.LogInformation("Promoted existing account {Username} to admin", username);
        }

        await db.SaveChangesAsync();
    }
}
