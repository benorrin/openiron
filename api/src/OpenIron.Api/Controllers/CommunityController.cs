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
[Route("api/community")]
public class CommunityController(AppDbContext db, ActivityService activity) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<ActionResult<CommunitySettingsDto>> GetSettings()
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == User.GetUserId());
        return user is null ? Unauthorized() : new CommunitySettingsDto(user.ShareActivity);
    }

    [HttpPut("settings")]
    public async Task<ActionResult<CommunitySettingsDto>> UpdateSettings(CommunitySettingsDto request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == User.GetUserId());
        if (user is null) return Unauthorized();

        user.ShareActivity = request.ShareActivity;
        await db.SaveChangesAsync();
        return new CommunitySettingsDto(user.ShareActivity);
    }

    /// <summary>Users who have chosen to share their activity, most active first.</summary>
    [HttpGet("users")]
    public async Task<List<CommunityMemberDto>> Members()
    {
        var members = await db.Users.AsNoTracking().Where(u => u.ShareActivity).ToListAsync();
        var workouts = (await activity.LoadWorkoutsAsync(members.Select(m => m.Id).ToList())).ToLookup(w => w.UserId);
        var now = DateTime.UtcNow;

        return members
            .Select(m => ToMember(m, ProgressCalculator.Summarise(workouts[m.Id].ToList(), now)))
            .OrderByDescending(m => m.WorkoutsLast30Days)
            .ThenByDescending(m => m.VolumeLast30DaysKg)
            .ThenBy(m => m.Username)
            .ToList();
    }

    /// <summary>
    /// A member's general progress. Users who aren't sharing get the same 404 as users who don't exist,
    /// so the endpoint can't be used to find out who has an account. You can always see your own.
    /// </summary>
    [HttpGet("users/{username}")]
    public async Task<ActionResult<CommunityProfileDto>> Profile(string username)
    {
        var normalized = username.ToLowerInvariant();
        var member = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedUsername == normalized);
        if (member is null || !(member.ShareActivity || member.Id == User.GetUserId())) return NotFound();

        var now = DateTime.UtcNow;
        var workouts = await activity.LoadWorkoutsAsync([member.Id]);

        var recent = await db.Workouts.AsNoTracking()
            .Where(w => w.UserId == member.Id)
            .OrderByDescending(w => w.PerformedAt)
            .Take(8)
            .Select(w => new
            {
                w.Name,
                w.PerformedAt,
                ExerciseCount = w.Exercises.Count,
                Sets = w.Exercises.SelectMany(e => e.Sets).Select(s => new { s.Reps, s.WeightKg }).ToList(),
            })
            .ToListAsync();

        var setRows = await (
            from s in db.Set<WorkoutSet>().AsNoTracking()
            join we in db.Set<WorkoutExercise>() on s.WorkoutExerciseId equals we.Id
            join w in db.Workouts on we.WorkoutId equals w.Id
            join e in db.Exercises on we.ExerciseId equals e.Id
            where w.UserId == member.Id
            select new { ExerciseId = e.Id, e.Name, e.MuscleGroup, WorkoutId = w.Id, s.Reps, s.WeightKg }).ToListAsync();

        var topLifts = setRows
            .GroupBy(r => r.ExerciseId)
            .Select(g => new TopLiftDto(
                g.First().Name,
                g.First().MuscleGroup,
                g.Max(r => r.WeightKg),
                Math.Round(g.Max(r => ProgressCalculator.Estimate1Rm(r.WeightKg, r.Reps)), 1),
                g.Select(r => r.WorkoutId).Distinct().Count()))
            .OrderByDescending(l => l.Sessions).ThenByDescending(l => l.Best1RmKg)
            .Take(6)
            .ToList();

        return new CommunityProfileDto(
            ToMember(member, ProgressCalculator.Summarise(workouts, now)),
            ProgressCalculator.Weekly(workouts.Select(w => (w.PerformedAt, w.VolumeKg)), now, 12),
            topLifts,
            recent.Select(w => new RecentWorkoutDto(
                w.Name, w.PerformedAt, w.ExerciseCount, w.Sets.Count, w.Sets.Sum(s => s.Reps * s.WeightKg))).ToList());
    }

    private CommunityMemberDto ToMember(User user, ActivitySummary s) => new(
        user.Username, user.Id == User.GetUserId(), s.TotalWorkouts, s.WorkoutsLast30Days, s.VolumeLast30DaysKg, s.LastWorkoutAt);
}
