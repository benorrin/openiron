using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Data;

namespace OpenIron.Api.Services;

public record WorkoutVolume(Guid UserId, DateTime PerformedAt, double VolumeKg);

public class ActivityService(AppDbContext db)
{
    /// <summary>Every workout for the given users with its total volume (sets × reps × weight).</summary>
    public async Task<List<WorkoutVolume>> LoadWorkoutsAsync(IReadOnlyCollection<Guid> userIds)
    {
        var rows = await db.Workouts.AsNoTracking()
            .Where(w => userIds.Contains(w.UserId))
            .Select(w => new
            {
                w.UserId,
                w.PerformedAt,
                Sets = w.Exercises.SelectMany(e => e.Sets).Select(s => new { s.Reps, s.WeightKg }).ToList(),
            })
            .ToListAsync();

        return rows.Select(r => new WorkoutVolume(r.UserId, r.PerformedAt, r.Sets.Sum(s => s.Reps * s.WeightKg))).ToList();
    }
}
