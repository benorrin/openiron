using System.ComponentModel.DataAnnotations;

namespace OpenIron.Api.Contracts;

public record ExerciseDto(Guid Id, string Name, string MuscleGroup, bool IsCustom);

public record ExerciseRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [Required, StringLength(50, MinimumLength = 1)] string MuscleGroup);
