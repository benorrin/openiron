using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Data;

namespace OpenIron.Api.Services;

public class AccountService(AppDbContext db)
{
    /// <summary>True if some other enabled admin exists, i.e. this admin can be removed without locking everyone out.</summary>
    public Task<bool> HasAnotherAdminAsync(Guid userId) =>
        db.Users.AnyAsync(u => u.IsAdmin && !u.IsDisabled && u.Id != userId);

    /// <summary>
    /// Permanently deletes a user and everything they own. Their data is removed explicitly, in dependency order,
    /// rather than leaning on a single cascade from the user row: workout and template rows hold a restricting
    /// reference to exercises, and PostgreSQL checks that immediately, so deleting the exercises first would fail.
    /// </summary>
    public async Task DeleteAsync(Guid userId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        await db.Workouts.Where(w => w.UserId == userId).ExecuteDeleteAsync();
        await db.WorkoutTemplates.Where(t => t.UserId == userId).ExecuteDeleteAsync();
        await db.BodyWeightEntries.Where(e => e.UserId == userId).ExecuteDeleteAsync();
        await db.Exercises.Where(e => e.UserId == userId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();

        await transaction.CommitAsync();
    }
}
