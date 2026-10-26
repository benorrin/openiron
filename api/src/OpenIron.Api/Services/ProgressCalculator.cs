using OpenIron.Api.Contracts;

namespace OpenIron.Api.Services;

public record ActivitySummary(int TotalWorkouts, int WorkoutsLast30Days, double VolumeLast30DaysKg, DateTime? LastWorkoutAt);

public static class ProgressCalculator
{
    public static ActivitySummary Summarise(IReadOnlyCollection<WorkoutVolume> workouts, DateTime now)
    {
        var recent = workouts.Where(w => w.PerformedAt >= now.AddDays(-30)).ToList();
        return new ActivitySummary(
            workouts.Count,
            recent.Count,
            recent.Sum(w => w.VolumeKg),
            workouts.Count == 0 ? null : workouts.Max(w => w.PerformedAt));
    }

    /// <summary>Epley estimate of the weight that could be lifted once. A single rep is its own 1RM.</summary>
    public static double Estimate1Rm(double weightKg, int reps) =>
        reps <= 1 ? weightKg : weightKg * (1 + reps / 30.0);

    public static ExercisePointDto ToPoint(Guid workoutId, DateTime performedAt, IReadOnlyCollection<SetDto> sets) =>
        new(workoutId, performedAt,
            TopWeightKg: sets.Max(s => s.WeightKg),
            Estimated1RmKg: Math.Round(sets.Max(s => Estimate1Rm(s.WeightKg, s.Reps)), 1),
            VolumeKg: sets.Sum(s => s.Reps * s.WeightKg),
            TotalReps: sets.Sum(s => s.Reps));

    public static PersonalRecordsDto Records(IReadOnlyCollection<ExercisePointDto> points)
    {
        if (points.Count == 0) return new PersonalRecordsDto(0, null, 0, null);

        // Earliest session wins ties so a record is credited to when it was first achieved.
        var heaviest = points.OrderByDescending(p => p.TopWeightKg).ThenBy(p => p.PerformedAt).First();
        var best1Rm = points.OrderByDescending(p => p.Estimated1RmKg).ThenBy(p => p.PerformedAt).First();
        return new PersonalRecordsDto(heaviest.TopWeightKg, heaviest.PerformedAt, best1Rm.Estimated1RmKg, best1Rm.PerformedAt);
    }

    public static DateOnly WeekStart(DateTime utc)
    {
        var date = DateOnly.FromDateTime(utc);
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    /// <summary>One bucket per week ending at <paramref name="now"/>, including empty weeks so charts have no gaps.</summary>
    public static List<WeeklyActivityDto> Weekly(
        IEnumerable<(DateTime PerformedAt, double VolumeKg)> workouts, DateTime now, int weeks)
    {
        var byWeek = workouts
            .GroupBy(w => WeekStart(w.PerformedAt))
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Volume: g.Sum(w => w.VolumeKg)));

        var current = WeekStart(now);
        return Enumerable.Range(0, weeks)
            .Select(i => current.AddDays(-7 * (weeks - 1 - i)))
            .Select(start => byWeek.TryGetValue(start, out var w)
                ? new WeeklyActivityDto(start, w.Count, w.Volume)
                : new WeeklyActivityDto(start, 0, 0))
            .ToList();
    }
}
