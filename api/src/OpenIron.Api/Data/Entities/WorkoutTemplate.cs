namespace OpenIron.Api.Data.Entities;

public class WorkoutTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<TemplateExercise> Exercises { get; set; } = [];
}

public class TemplateExercise
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkoutTemplateId { get; set; }
    public Guid ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;
    public int Position { get; set; }
    public int TargetSets { get; set; }
    public int TargetReps { get; set; }
}
