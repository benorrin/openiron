using OpenIron.Api.Contracts;
using OpenIron.Api.Data.Entities;
using OpenIron.Api.Services;

namespace OpenIron.Api.Tests;

public class NutritionCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 6, 30);

    // A 30-year-old, 180 cm, moderately active man. At 80 kg: BMR 1780, formula maintenance 1780 x 1.55 = 2759.
    private static PlanInput Input(
        IReadOnlyList<WeighIn> weighIns,
        GoalType goal = GoalType.Lose,
        double rate = 0.5,
        int? applied = null,
        int? appliedDaysAgo = null,
        double? targetWeight = null,
        Sex sex = Sex.Male,
        double height = 180) => new(
        sex, new DateOnly(1996, 6, 30), height, ActivityLevel.Moderate, goal, targetWeight, rate,
        applied, appliedDaysAgo is { } d ? Today.AddDays(-d) : null, weighIns, Today);

    /// <summary>One weigh-in a day for <paramref name="days"/> days ending today, moving <paramref name="kgPerWeek"/>.</summary>
    private static List<WeighIn> Daily(int days, double endKg, double kgPerWeek) =>
        Enumerable.Range(0, days + 1)
            .Select(i => new WeighIn(Today.AddDays(i - days), endKg - kgPerWeek * (days - i) / 7.0))
            .ToList();

    private static PlanDto Plan(PlanInput input) => NutritionCalculator.Compute(input)!;

    // --- building blocks ---

    [Theory]
    [InlineData(1996, 6, 30, 30)] // birthday today
    [InlineData(1996, 7, 1, 29)] // birthday tomorrow
    [InlineData(1996, 6, 29, 30)]
    [InlineData(2000, 2, 29, 26)] // leap-day birthday, not yet reached in a non-leap year
    public void Age_counts_whole_years_to_the_birthday(int y, int m, int d, int expected) =>
        Assert.Equal(expected, NutritionCalculator.AgeYears(new DateOnly(y, m, d), Today));

    [Fact]
    public void Bmr_matches_the_Mifflin_St_Jeor_equation()
    {
        Assert.Equal(1780, NutritionCalculator.Bmr(Sex.Male, 80, 180, 30), 6);
        Assert.Equal(1345.25, NutritionCalculator.Bmr(Sex.Female, 60, 165, 25), 6);
        // Unspecified sits halfway between the two constants (+5 and -161).
        Assert.Equal(1780 - 83, NutritionCalculator.Bmr(Sex.Unspecified, 80, 180, 30), 6);
    }

    [Fact]
    public void Activity_factors_rise_with_activity_and_floors_differ_by_sex()
    {
        var factors = Enum.GetValues<ActivityLevel>().Select(NutritionCalculator.ActivityFactor).ToList();
        Assert.Equal(factors.OrderBy(f => f), factors);
        Assert.True(NutritionCalculator.SafetyFloorKcal(Sex.Male) > NutritionCalculator.SafetyFloorKcal(Sex.Female));
    }

    [Fact]
    public void Trend_of_steady_weight_is_that_weight()
    {
        var trend = NutritionCalculator.Trend(Daily(20, 80, 0));
        Assert.All(trend, t => Assert.Equal(80, t.WeightKg, 9));
    }

    [Fact]
    public void Trend_lags_a_sudden_jump_and_then_catches_up()
    {
        var steady = Daily(10, 80, 0);
        var jump = Enumerable.Range(1, 30).Select(i => new WeighIn(Today.AddDays(i), 82)).ToList();
        var trend = NutritionCalculator.Trend(steady.Concat(jump).ToList());

        Assert.InRange(trend[11].WeightKg, 80, 80.3); // a day after a 2 kg jump, barely moved
        Assert.InRange(trend[^1].WeightKg, 81.9, 82); // a month later, there
    }

    [Fact]
    public void Trend_moves_further_across_a_longer_gap()
    {
        var oneDay = NutritionCalculator.Trend([new(Today, 80), new(Today.AddDays(1), 82)])[1].WeightKg;
        var oneWeek = NutritionCalculator.Trend([new(Today, 80), new(Today.AddDays(7), 82)])[1].WeightKg;
        Assert.True(oneWeek > oneDay);
    }

    [Fact]
    public void Slope_recovers_an_exact_weekly_rate_and_needs_two_dates()
    {
        Assert.Equal(-0.5, NutritionCalculator.WeeklySlope(Daily(21, 78, -0.5))!.Value, 9);
        Assert.Null(NutritionCalculator.WeeklySlope([new(Today, 80)]));
        Assert.Null(NutritionCalculator.WeeklySlope([new(Today, 80), new(Today, 81)]));
    }

    [Fact]
    public void No_weigh_ins_means_no_plan()
    {
        Assert.Null(NutritionCalculator.Compute(Input([])));
    }

    // --- the plain formula path ---

    [Fact]
    public void Without_an_applied_target_the_plan_is_formula_based()
    {
        var plan = Plan(Input(Daily(10, 80, 0)));

        Assert.Equal(1780, plan.BmrKcal);
        Assert.Equal(2759, plan.FormulaMaintenanceKcal);
        Assert.Equal(2759, plan.EstimatedMaintenanceKcal);
        Assert.Equal(0, plan.Confidence);
        Assert.Equal(2210, plan.TargetKcal); // 2759 - 550 for half a kilo a week, to the nearest 10
        Assert.Equal(ProgressStatus.NoPlan, plan.Progress.Status);
        Assert.Null(plan.Progress.SuggestedAdjustmentKcal);
        Assert.Equal(-0.5, plan.Progress.PlannedRateKgPerWeek);
    }

    [Theory]
    [InlineData(GoalType.Lose, 0.5, 2210)]
    [InlineData(GoalType.Gain, 0.25, 3030)] // 2759 + 275
    [InlineData(GoalType.Maintain, 0.5, 2760)] // the rate is ignored when maintaining
    public void The_target_is_maintenance_adjusted_by_the_goal_rate(GoalType goal, double rate, int expected) =>
        Assert.Equal(expected, Plan(Input(Daily(10, 80, 0), goal, rate)).TargetKcal);

    [Fact]
    public void Calories_are_based_on_the_smoothed_weight_not_a_single_weigh_in()
    {
        var steady = Daily(20, 80, 0);
        steady[^1] = new WeighIn(Today, 83); // one heavy morning

        var plan = Plan(Input(steady));
        Assert.InRange(plan.CurrentWeightKg, 80, 80.5);
    }

    // --- limits ---

    [Fact]
    public void Weight_loss_is_capped_at_one_percent_of_body_weight_a_week()
    {
        var plan = Plan(Input(Daily(10, 60, 0), rate: 1.0));

        Assert.True(plan.RateLimited);
        Assert.Equal(-0.6, plan.Progress.PlannedRateKgPerWeek);
    }

    [Fact]
    public void Weight_loss_is_capped_at_one_kilo_a_week_whatever_the_weight()
    {
        var plan = Plan(Input(Daily(10, 150, 0), rate: 1.0));
        Assert.False(plan.RateLimited);
        Assert.Equal(-1.0, plan.Progress.PlannedRateKgPerWeek);
    }

    [Fact]
    public void Weight_gain_is_capped_at_half_a_kilo_a_week()
    {
        var plan = Plan(Input(Daily(10, 80, 0), GoalType.Gain, 1.0));

        Assert.True(plan.RateLimited);
        Assert.Equal(0.5, plan.Progress.PlannedRateKgPerWeek);
    }

    [Fact]
    public void The_target_never_drops_below_the_safety_floor()
    {
        // A small woman (maintenance about 1670) asking for 0.45 kg a week would land at about 1170 without the floor.
        var plan = Plan(Input(Daily(10, 45, 0), sex: Sex.Female, height: 150, rate: 0.45));

        Assert.True(plan.LimitedBySafetyFloor);
        Assert.Equal(1200, plan.TargetKcal);
    }

    // --- the adaptive loop ---

    [Fact]
    public void Losing_at_the_planned_rate_is_on_track_and_the_target_holds()
    {
        // Followed 2210 for 40 days and lost exactly half a kilo a week: the formula was right.
        var plan = Plan(Input(Daily(40, 78, -0.5), applied: 2210, appliedDaysAgo: 40));

        Assert.Equal(ProgressStatus.OnTrack, plan.Progress.Status);
        Assert.Equal(-0.5, plan.Progress.ObservedRateKgPerWeek!.Value, 2);
        Assert.Null(plan.Progress.SuggestedAdjustmentKcal);
        Assert.Equal(1, plan.Confidence);
        Assert.InRange(plan.TargetKcal, 2210 - 80, 2210 + 80);
    }

    [Fact]
    public void Losing_too_slowly_lowers_the_suggested_target()
    {
        // Eating 2210 and losing only 0.1 kg/week means maintenance is about 2320, not 2759.
        var plan = Plan(Input(Daily(40, 78, -0.1), applied: 2210, appliedDaysAgo: 40));

        Assert.Equal(ProgressStatus.SlowerThanPlanned, plan.Progress.Status);
        Assert.Equal(2320, plan.EstimatedMaintenanceKcal);
        Assert.Equal(-440, plan.Progress.SuggestedAdjustmentKcal); // 2320 - 550 = 1770 vs 2210
        Assert.Equal(1770, plan.TargetKcal);
    }

    [Fact]
    public void The_suggested_change_is_exactly_the_gap_to_the_new_target()
    {
        foreach (var weeklyChange in new[] { -0.1, -0.2, -0.9, 0.3 })
        {
            var plan = Plan(Input(Daily(40, 79, weeklyChange), applied: 2210, appliedDaysAgo: 40));
            if (plan.Progress.SuggestedAdjustmentKcal is { } change)
                Assert.Equal(plan.TargetKcal - 2210, change);
        }
    }

    [Fact]
    public void Losing_too_fast_raises_the_suggested_target()
    {
        var plan = Plan(Input(Daily(40, 78, -0.9), applied: 2210, appliedDaysAgo: 40));

        Assert.Equal(ProgressStatus.FasterThanPlanned, plan.Progress.Status);
        Assert.Equal(440, plan.Progress.SuggestedAdjustmentKcal);
        Assert.Equal(2650, plan.TargetKcal); // 2210 + 990 = 3200 maintenance, less 550
    }

    [Fact]
    public void Gaining_while_trying_to_lose_is_flagged_and_still_respects_the_floor()
    {
        var plan = Plan(Input(Daily(40, 82, 0.3), applied: 2210, appliedDaysAgo: 40));

        Assert.Equal(ProgressStatus.WrongDirection, plan.Progress.Status);
        Assert.True(plan.LimitedBySafetyFloor);
        Assert.Equal(1500, plan.TargetKcal);
    }

    [Fact]
    public void Trust_in_the_scale_grows_with_how_much_data_there_is()
    {
        var early = Plan(Input(Daily(25, 79, -0.1), applied: 2210, appliedDaysAgo: 25));
        var late = Plan(Input(Daily(60, 79, -0.1), applied: 2210, appliedDaysAgo: 60));

        Assert.Equal(0.5, early.Confidence);
        Assert.Equal(1, late.Confidence);
        // Half-way between the formula and what the scale implies (2320) at 50% trust.
        var halfway = (early.FormulaMaintenanceKcal + 2320) / 2;
        Assert.InRange(early.EstimatedMaintenanceKcal, halfway - 2, halfway + 2);
        Assert.Equal(2320, late.EstimatedMaintenanceKcal);
    }

    [Fact]
    public void Infrequent_weigh_ins_earn_less_trust()
    {
        // Same 40 days, but only about one weigh-in a fortnight.
        var sparse = Daily(40, 79, -0.1).Where((_, i) => i % 9 == 0).ToList();
        var plan = Plan(Input(sparse, applied: 2210, appliedDaysAgo: 40));

        Assert.Equal(ProgressStatus.Gathering, plan.Progress.Status); // too few points to judge at all
        Assert.Equal(0, plan.Confidence);
    }

    [Fact]
    public void An_absurd_reading_cannot_drag_maintenance_far_from_the_formula()
    {
        var plan = Plan(Input(Daily(40, 92, 3.0), applied: 2210, appliedDaysAgo: 40));

        Assert.InRange(plan.EstimatedMaintenanceKcal, 0.65 * plan.FormulaMaintenanceKcal - 1, 1.35 * plan.FormulaMaintenanceKcal + 1);
    }

    [Fact]
    public void A_new_target_is_not_judged_until_there_is_enough_data()
    {
        var plan = Plan(Input(Daily(6, 80, -0.5), applied: 2210, appliedDaysAgo: 6));

        Assert.Equal(ProgressStatus.Gathering, plan.Progress.Status);
        Assert.Equal(5 + 14 - 6, plan.Progress.DaysUntilCheck);
        Assert.Null(plan.Progress.ObservedRateKgPerWeek);
        Assert.Null(plan.Progress.SuggestedAdjustmentKcal);
        Assert.Equal(2210, plan.TargetKcal); // still the formula figure
    }

    [Fact]
    public void Weigh_ins_from_before_the_target_began_are_not_attributed_to_it()
    {
        // 40 days of steady loss, but the target only started 3 days ago.
        var plan = Plan(Input(Daily(40, 78, -0.5), applied: 2210, appliedDaysAgo: 3));
        Assert.Equal(ProgressStatus.Gathering, plan.Progress.Status);
    }

    [Fact]
    public void Maintaining_flags_a_slow_drift_either_way()
    {
        var flat = Plan(Input(Daily(40, 80, 0), GoalType.Maintain, applied: 2760, appliedDaysAgo: 40));
        var up = Plan(Input(Daily(40, 82, 0.4), GoalType.Maintain, applied: 2760, appliedDaysAgo: 40));
        var down = Plan(Input(Daily(40, 78, -0.4), GoalType.Maintain, applied: 2760, appliedDaysAgo: 40));

        Assert.Equal(ProgressStatus.OnTrack, flat.Progress.Status);
        Assert.Equal(ProgressStatus.DriftingUp, up.Progress.Status);
        Assert.True(up.Progress.SuggestedAdjustmentKcal < 0);
        Assert.Equal(ProgressStatus.DriftingDown, down.Progress.Status);
        Assert.True(down.Progress.SuggestedAdjustmentKcal > 0);
    }

    [Fact]
    public void Only_the_last_eight_weeks_count()
    {
        // Gained for a long time, then lost at the planned pace for the last 8 weeks.
        var old = Enumerable.Range(0, 60).Select(i => new WeighIn(Today.AddDays(-120 + i), 70 + i * 0.1));
        var recent = Daily(56, 78, -0.5).Skip(1);
        var plan = Plan(Input(old.Concat(recent).ToList(), applied: 2210, appliedDaysAgo: 90));

        Assert.Equal(ProgressStatus.OnTrack, plan.Progress.Status);
    }

    // --- goal progress ---

    [Fact]
    public void The_goal_date_follows_from_the_pace_and_the_distance()
    {
        var plan = Plan(Input(Daily(10, 80, 0), targetWeight: 75));

        Assert.Equal(-5, plan.Progress.RemainingKg);
        Assert.False(plan.Progress.GoalReached);
        Assert.Equal(Today.AddDays(70), plan.Progress.EstimatedGoalDate); // 5 kg at 0.5 kg a week
    }

    [Fact]
    public void The_goal_date_uses_the_measured_pace_once_there_is_one()
    {
        // Losing 0.25 kg/wk rather than the planned 0.5 doubles the time to go: about 5.3 kg (the smoothed weight
        // runs slightly behind the scale) at 0.25 a week is about 150 days, not 75.
        var plan = Plan(Input(Daily(40, 80, -0.25), targetWeight: 75, applied: 2210, appliedDaysAgo: 40));
        Assert.InRange(plan.Progress.EstimatedGoalDate!.Value.DayNumber - Today.DayNumber, 140, 155);
    }

    [Fact]
    public void A_reached_goal_has_no_date()
    {
        var plan = Plan(Input(Daily(10, 74.5, 0), targetWeight: 75));

        Assert.True(plan.Progress.GoalReached);
        Assert.Null(plan.Progress.EstimatedGoalDate);
    }

    [Fact]
    public void Maintaining_has_no_goal_date_or_remaining_weight()
    {
        var plan = Plan(Input(Daily(10, 80, 0), GoalType.Maintain, targetWeight: 70));
        Assert.Null(plan.Progress.RemainingKg);
        Assert.Null(plan.Progress.EstimatedGoalDate);
    }

    // --- macros and trend output ---

    [Fact]
    public void Macros_add_back_up_to_the_target()
    {
        var plan = Plan(Input(Daily(10, 80, 0)));
        var kcal = plan.Macros.ProteinG * 4 + plan.Macros.CarbsG * 4 + plan.Macros.FatG * 9;

        Assert.Equal(160, plan.Macros.ProteinG); // 2 g per kg
        Assert.InRange(kcal, plan.TargetKcal - 10, plan.TargetKcal + 10);
    }

    [Fact]
    public void Protein_never_takes_more_than_forty_percent_of_calories()
    {
        // A heavy person on the floor: 2 g/kg would be well over 40% of 1500 kcal.
        var plan = Plan(Input(Daily(10, 140, 0), rate: 1.0));
        Assert.True(plan.Macros.ProteinG * 4 <= 0.4 * plan.TargetKcal + 2);
        Assert.True(plan.Macros.CarbsG >= 0);
    }

    [Fact]
    public void The_trend_returned_covers_the_last_ninety_days()
    {
        var plan = Plan(Input(Daily(200, 80, 0)));
        Assert.InRange(plan.Trend.Count, 90, 92);
        Assert.True(plan.Trend[0].Date >= Today.AddDays(-90));
    }
}
