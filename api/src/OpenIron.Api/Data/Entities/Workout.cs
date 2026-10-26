namespace OpenIron.Api.Data.Entities;

/// <summary>A logged workout session.</summary>
public class Workout
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>The template this session was started from, if any. Cleared if the template is deleted.</summary>
    public Guid? WorkoutTemplateId { get; set; }
    public required string Name { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    public int? DurationMinutes { get; set; }
    public string? Notes { get; set; }
    public List<WorkoutExercise> Exercises { get; set; } = [];
}

public class WorkoutExercise
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkoutId { get; set; }
    public Guid ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;
    public int Position { get; set; }
    public List<WorkoutSet> Sets { get; set; } = [];
}

public class WorkoutSet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkoutExerciseId { get; set; }
    public int Position { get; set; }
    public int Reps { get; set; }
    public double WeightKg { get; set; }
}
