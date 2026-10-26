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
[Route("api/exercises")]
public class ExercisesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<ExerciseDto>> List()
    {
        var exercises = await db.Exercises.VisibleTo(User.GetUserId())
            .OrderBy(e => e.MuscleGroup).ThenBy(e => e.Name)
            .ToListAsync();
        return exercises.Select(ToDto).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<ExerciseDto>> Create(ExerciseRequest request)
    {
        var exercise = new Exercise
        {
            UserId = User.GetUserId(),
            Name = request.Name.Trim(),
            MuscleGroup = request.MuscleGroup.Trim(),
        };
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(List), ToDto(exercise));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ExerciseDto>> Update(Guid id, ExerciseRequest request)
    {
        var exercise = await FindOwned(id);
        if (exercise is null) return NotFound();

        exercise.Name = request.Name.Trim();
        exercise.MuscleGroup = request.MuscleGroup.Trim();
        await db.SaveChangesAsync();
        return ToDto(exercise);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var exercise = await FindOwned(id);
        if (exercise is null) return NotFound();

        var inUse = await db.Set<TemplateExercise>().AnyAsync(x => x.ExerciseId == id)
                    || await db.Set<WorkoutExercise>().AnyAsync(x => x.ExerciseId == id);
        if (inUse)
            return Conflict(new ProblemDetails { Title = "This exercise is used by a template or logged workout." });

        db.Exercises.Remove(exercise);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // Built-in exercises have no owner, so they never match and can't be edited or deleted.
    private Task<Exercise?> FindOwned(Guid id) =>
        db.Exercises.FirstOrDefaultAsync(e => e.Id == id && e.UserId == User.GetUserId());

    private static ExerciseDto ToDto(Exercise e) => new(e.Id, e.Name, e.MuscleGroup, e.UserId is not null);
}
