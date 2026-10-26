using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Auth;
using OpenIron.Api.Contracts;
using OpenIron.Api.Data;
using OpenIron.Api.Data.Entities;
using OpenIron.Api.Services;

namespace OpenIron.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/progress")]
public class ProgressController(AppDbContext db, ActivityService activity) : ControllerBase
{
    /// <summary>Exercises the user has actually logged, most recently used first.</summary>
    [HttpGet("exercises")]
    public async Task<List<ProgressExerciseDto>> Exercises()
    {
        var userId = User.GetUserId();
        var rows = await (
            from we in db.Set<WorkoutExercise>().AsNoTracking()
            join w in db.Workouts on we.WorkoutId equals w.Id
            where w.UserId == userId
            select new { we.ExerciseId, w.PerformedAt }).ToListAsync();

        var ids = rows.Select(r => r.ExerciseId).Distinct().ToList();
        var exercises = await db.Exercises.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id);

        return rows.GroupBy(r => r.ExerciseId)
            .Select(g => new ProgressExerciseDto(
                g.Key, exercises[g.Key].Name, exercises[g.Key].MuscleGroup, g.Count(), g.Max(r => r.PerformedAt)))
            .OrderByDescending(e => e.LastPerformedAt)
            .ToList();
    }

    [HttpGet("exercises/{exerciseId:guid}")]
    public async Task<ActionResult<ExerciseProgressDto>> Exercise(Guid exerciseId)
    {
        var userId = User.GetUserId();
        var exercise = await db.Exercises.AsNoTracking().VisibleTo(userId).FirstOrDefaultAsync(e => e.Id == exerciseId);
        if (exercise is null) return NotFound();

        var sessions = await db.Workouts.AsNoTracking()
            .Where(w => w.UserId == userId && w.Exercises.Any(e => e.ExerciseId == exerciseId))
            .OrderBy(w => w.PerformedAt)
            .Select(w => new
            {
                w.Id,
                w.PerformedAt,
                Sets = w.Exercises.Where(e => e.ExerciseId == exerciseId)
                    .SelectMany(e => e.Sets).Select(s => new SetDto(s.Reps, s.WeightKg)).ToList(),
            })
            .ToListAsync();

        var points = sessions.Select(s => ProgressCalculator.ToPoint(s.Id, s.PerformedAt, s.Sets)).ToList();
        return new ExerciseProgressDto(exercise.Id, exercise.Name, exercise.MuscleGroup, ProgressCalculator.Records(points), points);
    }

    [HttpGet("overview")]
    public async Task<OverviewDto> Overview([FromQuery] int weeks = 12)
    {
        weeks = Math.Clamp(weeks, 1, 52);
        var userId = User.GetUserId();
        var now = DateTime.UtcNow;

        var workouts = await activity.LoadWorkoutsAsync([userId]);
        var summary = ProgressCalculator.Summarise(workouts, now);

        var weights = await db.BodyWeightEntries.AsNoTracking()
            .Where(e => e.UserId == userId).OrderBy(e => e.Date).Select(e => e.WeightKg).ToListAsync();

        return new OverviewDto(
            summary.TotalWorkouts,
            summary.WorkoutsLast30Days,
            weights.Count > 0 ? weights[^1] : null,
            weights.Count > 1 ? Math.Round(weights[^1] - weights[0], 1) : null,
            ProgressCalculator.Weekly(workouts.Select(w => (w.PerformedAt, w.VolumeKg)), now, weeks));
    }
}
