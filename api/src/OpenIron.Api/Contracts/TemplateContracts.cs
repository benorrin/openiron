using System.ComponentModel.DataAnnotations;

namespace OpenIron.Api.Contracts;

public record TemplateExerciseDto(Guid ExerciseId, string ExerciseName, string MuscleGroup, int TargetSets, int TargetReps);

public record TemplateDto(Guid Id, string Name, string? Notes, List<TemplateExerciseDto> Exercises);

public record TemplateExerciseRequest(
    Guid ExerciseId,
    [Range(1, 20)] int TargetSets,
    [Range(1, 100)] int TargetReps);

public record TemplateRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [StringLength(2000)] string? Notes,
    [Required, MinLength(1), MaxLength(30)] List<TemplateExerciseRequest> Exercises);
