namespace OpenIron.Api.Contracts;

/// <summary>An exercise the user has logged, for choosing what to chart.</summary>
public record ProgressExerciseDto(Guid ExerciseId, string Name, string MuscleGroup, int Sessions, DateTime LastPerformedAt);

/// <summary>One data point per workout in which the exercise was performed.</summary>
public record ExercisePointDto(
    Guid WorkoutId,
    DateTime PerformedAt,
    double TopWeightKg,
    double Estimated1RmKg,
    double VolumeKg,
    int TotalReps);

public record PersonalRecordsDto(double HeaviestWeightKg, DateTime? HeaviestOn, double Best1RmKg, DateTime? Best1RmOn);

public record ExerciseProgressDto(
    Guid ExerciseId,
    string Name,
    string MuscleGroup,
    PersonalRecordsDto Records,
    List<ExercisePointDto> Points);

public record WeeklyActivityDto(DateOnly WeekStart, int Workouts, double VolumeKg);

public record OverviewDto(
    int TotalWorkouts,
    int WorkoutsLast30Days,
    double? LatestBodyWeightKg,
    double? BodyWeightChangeKg,
    List<WeeklyActivityDto> Weeks);
