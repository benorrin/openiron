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
[Route("api/workouts")]
public class WorkoutsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<WorkoutSummaryDto>> List([FromQuery] int limit = 50, [FromQuery] int offset = 0)
    {
        limit = Math.Clamp(limit, 1, 200);
        var workouts = await db.Workouts.AsNoTracking()
            .Where(w => w.UserId == User.GetUserId())
            .OrderByDescending(w => w.PerformedAt)
            .Skip(Math.Max(offset, 0)).Take(limit)
            .Select(w => new
            {
                w.Id, w.Name, w.PerformedAt, w.DurationMinutes,
                ExerciseCount = w.Exercises.Count,
                Sets = w.Exercises.SelectMany(e => e.Sets).Select(s => new { s.Reps, s.WeightKg }).ToList(),
            })
            .ToListAsync();

        return workouts.Select(w => new WorkoutSummaryDto(
            w.Id, w.Name, w.PerformedAt, w.DurationMinutes, w.ExerciseCount, w.Sets.Count,
            w.Sets.Sum(s => s.Reps * s.WeightKg))).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkoutDto>> Get(Guid id)
    {
        var workout = await Query().AsNoTracking().FirstOrDefaultAsync(w => w.Id == id && w.UserId == User.GetUserId());
        return workout is null ? NotFound() : ToDto(workout);
    }

    /// <summary>Builds a starting point for a session from a template, pre-filled with the last weights used.</summary>
    [HttpGet("draft/{templateId:guid}")]
    public async Task<ActionResult<WorkoutDraftDto>> Draft(Guid templateId)
    {
        var userId = User.GetUserId();
        var template = await db.WorkoutTemplates.AsNoTracking()
            .Include(t => t.Exercises).ThenInclude(e => e.Exercise)
            .FirstOrDefaultAsync(t => t.Id == templateId && t.UserId == userId);
        if (template is null) return NotFound();

        var exercises = new List<WorkoutExerciseDto>();
        foreach (var te in template.Exercises.OrderBy(e => e.Position))
        {
            var exerciseId = te.ExerciseId;
            var previous = await db.Workouts.AsNoTracking()
                .Where(w => w.UserId == userId && w.Exercises.Any(e => e.ExerciseId == exerciseId))
                .OrderByDescending(w => w.PerformedAt)
                .Take(1)
                .SelectMany(w => w.Exercises.Where(e => e.ExerciseId == exerciseId))
                .SelectMany(e => e.Sets)
                .OrderBy(s => s.Position)
                .Select(s => s.WeightKg)
                .ToListAsync();

            var sets = Enumerable.Range(0, te.TargetSets).Select(i =>
            {
                var weight = previous.Count > 0 ? previous[Math.Min(i, previous.Count - 1)] : 0;
                return new SetDto(te.TargetReps, weight);
            }).ToList();

            exercises.Add(new WorkoutExerciseDto(te.ExerciseId, te.Exercise.Name, te.Exercise.MuscleGroup, sets));
        }

        return new WorkoutDraftDto(template.Id, template.Name, exercises);
    }

    [HttpPost]
    public async Task<ActionResult<WorkoutDto>> Create(WorkoutRequest request)
    {
        var userId = User.GetUserId();
        if (!await ReferencesAreValid(request, userId)) return BadRequest(Problem("One or more exercises or the template do not exist."));

        var workout = new Workout { UserId = userId, Name = request.Name.Trim() };
        Apply(workout, request);
        db.Workouts.Add(workout);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = workout.Id }, await Load(workout.Id));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkoutDto>> Update(Guid id, WorkoutRequest request)
    {
        var userId = User.GetUserId();
        var workout = await Query().FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);
        if (workout is null) return NotFound();
        if (!await ReferencesAreValid(request, userId)) return BadRequest(Problem("One or more exercises or the template do not exist."));

        // See TemplatesController.Update: replacements with client-generated keys must be added explicitly.
        db.Set<WorkoutExercise>().RemoveRange(workout.Exercises);
        Apply(workout, request);
        db.Set<WorkoutExercise>().AddRange(workout.Exercises);
        await db.SaveChangesAsync();

        return await Load(id);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var workout = await db.Workouts.FirstOrDefaultAsync(w => w.Id == id && w.UserId == User.GetUserId());
        if (workout is null) return NotFound();

        db.Workouts.Remove(workout);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private IQueryable<Workout> Query() =>
        db.Workouts
            .Include(w => w.Exercises.OrderBy(e => e.Position)).ThenInclude(e => e.Exercise)
            .Include(w => w.Exercises).ThenInclude(e => e.Sets.OrderBy(s => s.Position));

    private async Task<WorkoutDto> Load(Guid id) => ToDto(await Query().AsNoTracking().FirstAsync(w => w.Id == id));

    private static void Apply(Workout workout, WorkoutRequest request)
    {
        workout.Name = request.Name.Trim();
        workout.PerformedAt = NormalizeUtc(request.PerformedAt ?? DateTime.UtcNow);
        workout.DurationMinutes = request.DurationMinutes;
        workout.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        workout.WorkoutTemplateId = request.TemplateId;
        workout.Exercises = request.Exercises.Select((e, i) => new WorkoutExercise
        {
            ExerciseId = e.ExerciseId,
            Position = i,
            Sets = e.Sets.Select((s, j) => new WorkoutSet { Position = j, Reps = s.Reps, WeightKg = s.WeightKg }).ToList(),
        }).ToList();
    }

    private async Task<bool> ReferencesAreValid(WorkoutRequest request, Guid userId)
    {
        var ids = request.Exercises.Select(e => e.ExerciseId).Distinct().ToList();
        if (await db.Exercises.VisibleTo(userId).CountAsync(e => ids.Contains(e.Id)) != ids.Count) return false;

        return request.TemplateId is null
               || await db.WorkoutTemplates.AnyAsync(t => t.Id == request.TemplateId && t.UserId == userId);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    private static ProblemDetails Problem(string title) => new() { Title = title };

    private static WorkoutDto ToDto(Workout w)
    {
        var exercises = w.Exercises.OrderBy(e => e.Position).Select(e => new WorkoutExerciseDto(
            e.ExerciseId, e.Exercise.Name, e.Exercise.MuscleGroup,
            e.Sets.OrderBy(s => s.Position).Select(s => new SetDto(s.Reps, s.WeightKg)).ToList())).ToList();

        return new WorkoutDto(w.Id, w.WorkoutTemplateId, w.Name, w.PerformedAt, w.DurationMinutes, w.Notes,
            exercises.SelectMany(e => e.Sets).Sum(s => s.Reps * s.WeightKg), exercises);
    }
}
