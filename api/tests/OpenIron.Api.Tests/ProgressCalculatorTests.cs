using OpenIron.Api.Contracts;
using OpenIron.Api.Services;

namespace OpenIron.Api.Tests;

public class ProgressCalculatorTests
{
    [Theory]
    [InlineData(100, 1, 100)]
    [InlineData(100, 5, 116.666666)]
    [InlineData(60, 10, 80)]
    [InlineData(0, 12, 0)]
    public void Estimate1Rm_uses_the_Epley_formula(double kg, int reps, double expected) =>
        Assert.Equal(expected, ProgressCalculator.Estimate1Rm(kg, reps), 4);

    [Fact]
    public void ToPoint_summarises_a_session()
    {
        var point = ProgressCalculator.ToPoint(Guid.NewGuid(), DateTime.UtcNow,
            [new SetDto(5, 100), new SetDto(3, 110), new SetDto(10, 60)]);

        Assert.Equal(110, point.TopWeightKg);
        Assert.Equal(121, point.Estimated1RmKg);
        Assert.Equal(500 + 330 + 600, point.VolumeKg);
        Assert.Equal(18, point.TotalReps);
    }

    [Fact]
    public void Records_credit_the_earliest_session_on_ties()
    {
        var early = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var late = early.AddDays(7);
        var records = ProgressCalculator.Records([
            new ExercisePointDto(Guid.NewGuid(), late, 100, 110, 0, 0),
            new ExercisePointDto(Guid.NewGuid(), early, 100, 105, 0, 0),
        ]);

        Assert.Equal(100, records.HeaviestWeightKg);
        Assert.Equal(early, records.HeaviestOn);
        Assert.Equal(110, records.Best1RmKg);
        Assert.Equal(late, records.Best1RmOn);
    }

    [Fact]
    public void Records_for_no_data_are_empty() =>
        Assert.Equal(new PersonalRecordsDto(0, null, 0, null), ProgressCalculator.Records([]));

    [Theory]
    [InlineData(2026, 3, 2, 2026, 3, 2)]   // Monday
    [InlineData(2026, 3, 8, 2026, 3, 2)]   // Sunday belongs to the week that started the Monday before
    [InlineData(2026, 3, 4, 2026, 3, 2)]
    public void WeekStart_is_the_preceding_monday(int y, int m, int d, int ey, int em, int ed) =>
        Assert.Equal(new DateOnly(ey, em, ed), ProgressCalculator.WeekStart(new DateTime(y, m, d, 12, 0, 0, DateTimeKind.Utc)));

    [Fact]
    public void Weekly_fills_empty_weeks_and_ignores_older_workouts()
    {
        var now = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc); // Wednesday, week of 16 March
        var weeks = ProgressCalculator.Weekly([
            (new DateTime(2026, 3, 16, 8, 0, 0, DateTimeKind.Utc), 1000),
            (new DateTime(2026, 3, 17, 8, 0, 0, DateTimeKind.Utc), 500),
            (new DateTime(2026, 3, 3, 8, 0, 0, DateTimeKind.Utc), 700),
            (new DateTime(2025, 1, 1, 8, 0, 0, DateTimeKind.Utc), 9999),
        ], now, 3);

        Assert.Equal([new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 16)], weeks.Select(w => w.WeekStart));
        Assert.Equal([1, 0, 2], weeks.Select(w => w.Workouts));
        Assert.Equal([700, 0, 1500], weeks.Select(w => w.VolumeKg));
    }
}
