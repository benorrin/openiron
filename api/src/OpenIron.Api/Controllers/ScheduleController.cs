using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Auth;
using OpenIron.Api.Contracts;
using OpenIron.Api.Data;
using OpenIron.Api.Data.Entities;

namespace OpenIron.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/schedule")]
public class ScheduleController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ScheduleDto> Get() => await Build();

    /// <summary>
    /// Replaces the weekly schedule. Which weekdays are scheduled decides when adherence starts being measured, so
    /// changing only a template or the step goal keeps that date, but adding or removing a day moves it to today.
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<ScheduleDto>> Save(ScheduleRequest request, [FromQuery] int tz = 0)
    {
        if (!LocalTime.IsValid(tz)) return BadRequest(new ProblemDetails { Title = "Invalid time zone offset." });
        var userId = User.GetUserId();

        if (request.Days.Select(d => d.DayOfWeek).Distinct().Count() != request.Days.Count)
            return BadRequest(new ProblemDetails { Title = "Each weekday can only be scheduled once." });
        if (request.Days.Any(d => !Enum.IsDefined(d.DayOfWeek)))
            return BadRequest(new ProblemDetails { Title = "Unknown weekday." });

        var templateIds = request.Days.Where(d => d.TemplateId is not null).Select(d => d.TemplateId!.Value).Distinct().ToList();
        if (await db.WorkoutTemplates.CountAsync(t => t.UserId == userId && templateIds.Contains(t.Id)) != templateIds.Count)
            return BadRequest(new ProblemDetails { Title = "One or more templates do not exist." });

        var existing = await db.ScheduledDays.Where(d => d.UserId == userId).ToListAsync();
        var settings = await db.ConsistencySettings.FirstOrDefaultAsync(s => s.UserId == userId);
        var sameDays = existing.Select(d => d.DayOfWeek).Order().SequenceEqual(request.Days.Select(d => d.DayOfWeek).Order());

        if (settings is null)
        {
            settings = new ConsistencySettings { UserId = userId };
            db.ConsistencySettings.Add(settings);
        }
        if (settings.ScheduleSince == default || !sameDays) settings.ScheduleSince = LocalTime.Today(tz);
        settings.StepGoal = request.StepGoal;

        // Update in place rather than delete and re-add: a weekday is part of the key, so a replacement row
        // would collide with the one it replaces.
        var requested = request.Days.ToDictionary(d => d.DayOfWeek);
        db.ScheduledDays.RemoveRange(existing.Where(d => !requested.ContainsKey(d.DayOfWeek)));
        foreach (var (day, wanted) in requested)
        {
            var row = existing.FirstOrDefault(d => d.DayOfWeek == day);
            if (row is null) db.ScheduledDays.Add(new ScheduledDay { UserId = userId, DayOfWeek = day, WorkoutTemplateId = wanted.TemplateId });
            else row.WorkoutTemplateId = wanted.TemplateId;
        }
        await db.SaveChangesAsync();

        return await Build();
    }

    private async Task<ScheduleDto> Build()
    {
        var userId = User.GetUserId();
        var days = await db.ScheduledDays.AsNoTracking()
            .Where(d => d.UserId == userId)
            .Select(d => new ScheduledDayDto(
                d.DayOfWeek,
                d.WorkoutTemplateId,
                db.WorkoutTemplates.Where(t => t.Id == d.WorkoutTemplateId).Select(t => t.Name).FirstOrDefault()))
            .ToListAsync();
        var settings = await db.ConsistencySettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId);

        // Monday first, to match how weeks are shown everywhere else.
        return new ScheduleDto(
            days.OrderBy(d => ((int)d.DayOfWeek + 6) % 7).ToList(),
            settings?.StepGoal ?? ConsistencySettings.DefaultStepGoal,
            days.Count > 0 ? settings?.ScheduleSince : null);
    }
}
