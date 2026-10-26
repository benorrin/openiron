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
[Route("api/consistency")]
public class ConsistencyController(AppDbContext db) : ControllerBase
{
    // Enough history for the heatmap (53 weeks) and the longest streak window, with room to spare.
    private const int HistoryDays = 400;

    /// <param name="tz">The browser's UTC offset (JavaScript's getTimezoneOffset), so days fall on the user's own calendar.</param>
    [HttpGet]
    public async Task<ActionResult<ConsistencyDto>> Get([FromQuery] int tz = 0)
    {
        if (!LocalTime.IsValid(tz)) return BadRequest(new ProblemDetails { Title = "Invalid time zone offset." });

        var userId = User.GetUserId();
        var today = LocalTime.Today(tz);
        var since = today.AddDays(-HistoryDays);
        var sinceUtc = since.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(tz).AddDays(-1);

        var performed = await db.Workouts.AsNoTracking()
            .Where(w => w.UserId == userId && w.PerformedAt >= sinceUtc)
            .Select(w => w.PerformedAt)
            .ToListAsync();
        var activities = await db.Activities.AsNoTracking().Where(a => a.UserId == userId && a.Date >= since).ToListAsync();

        var days = new Dictionary<DateOnly, DayRecord>();
        void Add(DateOnly date, int workouts = 0, int sessions = 0, int minutes = 0, int? steps = null)
        {
            var current = days.GetValueOrDefault(date) ?? new DayRecord(0, 0, 0, null);
            days[date] = new DayRecord(
                current.Workouts + workouts, current.Activities + sessions, current.Minutes + minutes, steps ?? current.Steps);
        }

        foreach (var at in performed) Add(LocalTime.DateOf(at, tz), workouts: 1);
        foreach (var a in activities)
        {
            if (a.Type == ActivityType.Steps) Add(a.Date, steps: a.Steps);
            else Add(a.Date, sessions: 1, minutes: a.DurationMinutes ?? 0);
        }

        var scheduled = await db.ScheduledDays.AsNoTracking()
            .Where(d => d.UserId == userId)
            .Select(d => new
            {
                d.DayOfWeek,
                d.WorkoutTemplateId,
                TemplateName = db.WorkoutTemplates.Where(t => t.Id == d.WorkoutTemplateId).Select(t => t.Name).FirstOrDefault(),
            })
            .ToListAsync();
        var settings = await db.ConsistencySettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId);
        var joined = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.CreatedAt).SingleAsync();

        var trackingStart = LocalTime.DateOf(joined, tz);
        if (days.Count > 0 && days.Keys.Min() < trackingStart) trackingStart = days.Keys.Min();

        return Compute(today, settings, scheduled.ToDictionary(d => d.DayOfWeek, d => new ScheduleSlot(d.WorkoutTemplateId, d.TemplateName)), days, trackingStart);
    }

    private static ConsistencyDto Compute(
        DateOnly today, ConsistencySettings? settings, Dictionary<DayOfWeek, ScheduleSlot> schedule,
        Dictionary<DateOnly, DayRecord> days, DateOnly trackingStart) =>
        ConsistencyCalculator.Compute(new ConsistencyInput(
            today,
            settings?.StepGoal ?? ConsistencySettings.DefaultStepGoal,
            schedule,
            days,
            trackingStart,
            settings?.ScheduleSince is { } s && s != default ? s : today));
}
