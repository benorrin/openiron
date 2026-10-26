namespace OpenIron.Api.Contracts;

public record CommunitySettingsDto(bool ShareActivity);

public record CommunityMemberDto(
    string Username,
    bool IsYou,
    int TotalWorkouts,
    int WorkoutsLast30Days,
    double VolumeLast30DaysKg,
    DateTime? LastWorkoutAt);

public record TopLiftDto(string ExerciseName, string MuscleGroup, double HeaviestWeightKg, double Best1RmKg, int Sessions);

public record RecentWorkoutDto(string Name, DateTime PerformedAt, int ExerciseCount, int SetCount, double TotalVolumeKg);

public record CommunityProfileDto(
    CommunityMemberDto Member,
    List<WeeklyActivityDto> Weeks,
    List<TopLiftDto> TopLifts,
    List<RecentWorkoutDto> RecentWorkouts);
