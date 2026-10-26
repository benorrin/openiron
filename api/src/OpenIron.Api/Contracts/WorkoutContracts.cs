using System.ComponentModel.DataAnnotations;

namespace OpenIron.Api.Contracts;

public record SetDto(int Reps, double WeightKg);

public record WorkoutExerciseDto(Guid ExerciseId, string ExerciseName, string MuscleGroup, List<SetDto> Sets);

public record WorkoutDto(
    Guid Id,
    Guid? TemplateId,
    string Name,
    DateTime PerformedAt,
    int? DurationMinutes,
    string? Notes,
    double TotalVolumeKg,
    List<WorkoutExerciseDto> Exercises);

public record WorkoutSummaryDto(
    Guid Id,
    string Name,
    DateTime PerformedAt,
    int? DurationMinutes,
    int ExerciseCount,
    int SetCount,
    double TotalVolumeKg);

public record SetRequest([Range(1, 500)] int Reps, [Range(0, 1000)] double WeightKg);

public record WorkoutExerciseRequest(
    Guid ExerciseId,
    [Required, MinLength(1), MaxLength(50)] List<SetRequest> Sets);

public record WorkoutRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    DateTime? PerformedAt,
    [Range(1, 1440)] int? DurationMinutes,
    [StringLength(2000)] string? Notes,
    Guid? TemplateId,
    [Required, MinLength(1), MaxLength(30)] List<WorkoutExerciseRequest> Exercises);

/// <summary>A pre-filled workout for starting a session from a template, using the user's last weights.</summary>
public record WorkoutDraftDto(Guid TemplateId, string Name, List<WorkoutExerciseDto> Exercises);
