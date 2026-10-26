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
[Route("api/body-weight")]
public class BodyWeightController(AppDbContext db) : ControllerBase
{
    /// <summary>The user's entries, oldest first so they can be charted directly.</summary>
    [HttpGet]
    public async Task<List<BodyWeightDto>> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var query = db.BodyWeightEntries.AsNoTracking().Where(e => e.UserId == User.GetUserId());
        if (from is not null) query = query.Where(e => e.Date >= from);
        if (to is not null) query = query.Where(e => e.Date <= to);

        var entries = await query.OrderBy(e => e.Date).ToListAsync();
        return entries.Select(ToDto).ToList();
    }

    /// <summary>Logs a weigh-in. There is one entry per day, so logging the same day again replaces it.</summary>
    [HttpPost]
    public async Task<ActionResult<BodyWeightDto>> Log(BodyWeightRequest request)
    {
        var userId = User.GetUserId();
        var entry = await db.BodyWeightEntries.FirstOrDefaultAsync(e => e.UserId == userId && e.Date == request.Date);
        var created = entry is null;

        if (entry is null)
        {
            entry = new BodyWeightEntry { UserId = userId, Date = request.Date };
            db.BodyWeightEntries.Add(entry);
        }

        entry.WeightKg = request.WeightKg;
        await db.SaveChangesAsync();

        return created ? CreatedAtAction(nameof(List), ToDto(entry)) : ToDto(entry);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entry = await db.BodyWeightEntries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == User.GetUserId());
        if (entry is null) return NotFound();

        db.BodyWeightEntries.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static BodyWeightDto ToDto(BodyWeightEntry e) => new(e.Id, e.Date, e.WeightKg);
}
