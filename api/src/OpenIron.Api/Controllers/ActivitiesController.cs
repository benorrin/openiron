using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Auth;
using OpenIron.Api.Contracts;
using OpenIron.Api.Data;
using OpenIron.Api.Data.Entities;

namespace OpenIron.Api.Controllers;

/// <summary>Walks, runs, treadmill sessions and daily step counts: everything that counts toward consistency besides lifting.</summary>
[ApiController]
[Authorize]
[Route("api/activities")]
public class ActivitiesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<ActivityDto>> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var userId = User.GetUserId();
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-90);

        var query = db.Activities.AsNoTracking().Where(a => a.UserId == userId && a.Date >= start);
        if (to is not null) query = query.Where(a => a.Date <= to);

        var rows = await query.OrderByDescending(a => a.Date).ThenByDescending(a => a.CreatedAt).Take(500).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<ActivityDto>> Create(ActivityRequest request)
    {
        if (Validate(request) is { } problem) return problem;
        var userId = User.GetUserId();

        // A day has one step count, so logging it again replaces the first rather than adding a second.
        var entry = request.Type == ActivityType.Steps
            ? await db.Activities.FirstOrDefaultAsync(a => a.UserId == userId && a.Date == request.Date && a.Type == ActivityType.Steps)
            : null;
        var created = entry is null;

        if (entry is null)
        {
            entry = new ActivityEntry { UserId = userId };
            db.Activities.Add(entry);
        }

        Apply(entry, request);
        await db.SaveChangesAsync();

        return created ? CreatedAtAction(nameof(List), ToDto(entry)) : ToDto(entry);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ActivityDto>> Update(Guid id, ActivityRequest request)
    {
        if (Validate(request) is { } problem) return problem;
        var userId = User.GetUserId();

        var entry = await db.Activities.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (entry is null) return NotFound();

        if (request.Type == ActivityType.Steps
            && await db.Activities.AnyAsync(a => a.UserId == userId && a.Id != id && a.Date == request.Date && a.Type == ActivityType.Steps))
            return Conflict(new ProblemDetails { Title = "You already logged steps for that day." });

        Apply(entry, request);
        await db.SaveChangesAsync();
        return ToDto(entry);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entry = await db.Activities.FirstOrDefaultAsync(a => a.Id == id && a.UserId == User.GetUserId());
        if (entry is null) return NotFound();

        db.Activities.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private ObjectResult? Validate(ActivityRequest request)
    {
        // The browser's idea of "today" can be a day ahead of UTC, so allow tomorrow but no further.
        if (request.Date > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            return BadRequest(new ProblemDetails { Title = "You can't log activity in the future." });

        if (request.Type == ActivityType.Steps)
            return request.Steps is null ? BadRequest(new ProblemDetails { Title = "Enter how many steps you took." }) : null;

        return request.DurationMinutes is null ? BadRequest(new ProblemDetails { Title = "Enter how long it lasted, in minutes." }) : null;
    }

    private static void Apply(ActivityEntry entry, ActivityRequest request)
    {
        var steps = request.Type == ActivityType.Steps;
        entry.Date = request.Date;
        entry.Type = request.Type;
        // Each kind only keeps the fields that mean something for it.
        entry.Steps = steps ? request.Steps : null;
        entry.DurationMinutes = steps ? null : request.DurationMinutes;
        entry.DistanceKm = steps ? null : request.DistanceKm;
        entry.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
    }

    private static ActivityDto ToDto(ActivityEntry a) => new(a.Id, a.Date, a.Type, a.DurationMinutes, a.DistanceKm, a.Steps, a.Notes);
}
