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
[Route("api/templates")]
public class TemplatesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<TemplateDto>> List()
    {
        var templates = await Query().Where(t => t.UserId == User.GetUserId())
            .OrderBy(t => t.Name)
            .ToListAsync();
        return templates.Select(ToDto).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TemplateDto>> Get(Guid id)
    {
        var template = await Query().FirstOrDefaultAsync(t => t.Id == id && t.UserId == User.GetUserId());
        return template is null ? NotFound() : ToDto(template);
    }

    [HttpPost]
    public async Task<ActionResult<TemplateDto>> Create(TemplateRequest request)
    {
        if (!await ExercisesAreVisible(request)) return InvalidExercises();

        var template = new WorkoutTemplate { UserId = User.GetUserId(), Name = request.Name.Trim() };
        Apply(template, request);
        db.WorkoutTemplates.Add(template);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = template.Id }, await Load(template.Id));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TemplateDto>> Update(Guid id, TemplateRequest request)
    {
        var template = await Query().FirstOrDefaultAsync(t => t.Id == id && t.UserId == User.GetUserId());
        if (template is null) return NotFound();
        if (!await ExercisesAreVisible(request)) return InvalidExercises();

        // Entities with client-generated keys that are only reachable through a navigation are assumed to
        // exist already, so the replacements must be added to the context explicitly.
        db.Set<TemplateExercise>().RemoveRange(template.Exercises);
        Apply(template, request);
        db.Set<TemplateExercise>().AddRange(template.Exercises);
        await db.SaveChangesAsync();

        return await Load(id);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var template = await db.WorkoutTemplates.FirstOrDefaultAsync(t => t.Id == id && t.UserId == User.GetUserId());
        if (template is null) return NotFound();

        db.WorkoutTemplates.Remove(template);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private IQueryable<WorkoutTemplate> Query() =>
        db.WorkoutTemplates.Include(t => t.Exercises.OrderBy(e => e.Position)).ThenInclude(e => e.Exercise);

    private async Task<TemplateDto> Load(Guid id) =>
        ToDto(await Query().AsNoTracking().FirstAsync(t => t.Id == id));

    private static void Apply(WorkoutTemplate template, TemplateRequest request)
    {
        template.Name = request.Name.Trim();
        template.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        template.Exercises = request.Exercises.Select((e, i) => new TemplateExercise
        {
            ExerciseId = e.ExerciseId,
            Position = i,
            TargetSets = e.TargetSets,
            TargetReps = e.TargetReps,
        }).ToList();
    }

    private async Task<bool> ExercisesAreVisible(TemplateRequest request)
    {
        var ids = request.Exercises.Select(e => e.ExerciseId).Distinct().ToList();
        return await db.Exercises.VisibleTo(User.GetUserId()).CountAsync(e => ids.Contains(e.Id)) == ids.Count;
    }

    private BadRequestObjectResult InvalidExercises() =>
        BadRequest(new ProblemDetails { Title = "One or more exercises do not exist." });

    private static TemplateDto ToDto(WorkoutTemplate t) => new(
        t.Id, t.Name, t.Notes,
        t.Exercises.OrderBy(e => e.Position)
            .Select(e => new TemplateExerciseDto(e.ExerciseId, e.Exercise.Name, e.Exercise.MuscleGroup, e.TargetSets, e.TargetReps))
            .ToList());
}
