using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Auth;
using OpenIron.Api.Contracts;
using OpenIron.Api.Data;
using OpenIron.Api.Data.Entities;
using OpenIron.Api.Services;

namespace OpenIron.Api.Controllers;

/// <summary>
/// A calorie target from a profile and goal, adjusted by what the user's weigh-ins show. Private to the user:
/// nothing here appears in community profiles or the admin panel.
/// </summary>
[ApiController]
[Authorize]
[Route("api/nutrition")]
public class NutritionController(AppDbContext db) : ControllerBase
{
    private const int MinAge = 18;
    private const int MaxAge = 100;

    [HttpGet]
    public async Task<NutritionDto> Get() => await Build(await Load());

    [HttpPut("settings")]
    public async Task<ActionResult<NutritionDto>> Save(NutritionSettingsRequest request)
    {
        var age = NutritionCalculator.AgeYears(request.DateOfBirth, Today());
        if (age is < MinAge or > MaxAge)
            return BadRequest(new ProblemDetails { Title = $"Calorie targets are only calculated for ages {MinAge} to {MaxAge}." });

        var maintaining = request.Goal == GoalType.Maintain;
        if (!maintaining && request.TargetWeightKg is null)
            return BadRequest(new ProblemDetails { Title = "Enter the weight you're aiming for." });
        if (!maintaining && request.WeeklyRateKg < 0.05)
            return BadRequest(new ProblemDetails { Title = "Choose how fast you want to change, at least 0.05 kg a week." });

        var settings = await Load();
        if (settings is null)
        {
            settings = new NutritionSettings { UserId = User.GetUserId() };
            db.NutritionSettings.Add(settings);
        }

        settings.Sex = request.Sex;
        settings.DateOfBirth = request.DateOfBirth;
        settings.HeightCm = request.HeightCm;
        settings.Activity = request.Activity;
        settings.Goal = request.Goal;
        settings.TargetWeightKg = maintaining ? null : request.TargetWeightKg;
        settings.WeeklyRateKg = maintaining ? 0 : request.WeeklyRateKg;
        settings.UpdatedAt = DateTime.UtcNow;
        // The applied target is deliberately left alone: what someone has been eating, and how their weight
        // responded, is still true after they change their goal.
        await db.SaveChangesAsync();

        return await Build(settings);
    }

    /// <summary>
    /// Records the calories the user is now aiming for, which starts a fresh check on how their weight responds.
    /// With no body, that is the current recommendation; otherwise it's their own number, for when they eat something different.
    /// </summary>
    [HttpPost("apply")]
    public async Task<ActionResult<NutritionDto>> Apply(ApplyTargetRequest request)
    {
        var settings = await Load();
        if (settings is null)
            return BadRequest(new ProblemDetails { Title = "Set up your profile and goal first." });

        var calories = request.Calories;
        if (calories is null)
        {
            var plan = (await Build(settings)).Plan;
            if (plan is null)
                return BadRequest(new ProblemDetails { Title = "Log a weigh-in first so there is a target to apply." });
            calories = plan.TargetKcal;
        }

        settings.AppliedKcal = calories;
        settings.AppliedOn = Today();
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return await Build(settings);
    }

    /// <summary>Stops following a target, so nothing is judged against it until a new one is applied.</summary>
    [HttpDelete("apply")]
    public async Task<ActionResult<NutritionDto>> ClearApplied()
    {
        var settings = await Load();
        if (settings is null) return NotFound();

        settings.AppliedKcal = null;
        settings.AppliedOn = null;
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return await Build(settings);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private Task<NutritionSettings?> Load() => db.NutritionSettings.FirstOrDefaultAsync(s => s.UserId == User.GetUserId());

    private async Task<NutritionDto> Build(NutritionSettings? settings)
    {
        if (settings is null) return new NutritionDto(null, null, "needsSettings");

        var dto = new NutritionSettingsDto(
            settings.Sex, settings.DateOfBirth, settings.HeightCm, settings.Activity, settings.Goal,
            settings.TargetWeightKg, settings.WeeklyRateKg, settings.AppliedKcal, settings.AppliedOn);

        var weighIns = (await db.BodyWeightEntries.AsNoTracking()
                .Where(e => e.UserId == settings.UserId)
                .OrderBy(e => e.Date)
                .ToListAsync())
            .Select(e => new WeighIn(e.Date, e.WeightKg))
            .ToList();

        var plan = NutritionCalculator.Compute(new PlanInput(
            settings.Sex, settings.DateOfBirth, settings.HeightCm, settings.Activity, settings.Goal,
            settings.TargetWeightKg, settings.WeeklyRateKg, settings.AppliedKcal, settings.AppliedOn, weighIns, Today()));

        return new NutritionDto(dto, plan, plan is null ? "needsWeighIn" : null);
    }
}
