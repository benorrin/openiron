using OpenIron.Api.Contracts;
using OpenIron.Api.Services;

namespace OpenIron.Api.Tests;

public class ConsistencyCalculatorTests
{
    // A Wednesday. The week runs Monday 6 July to Sunday 12 July.
    private static readonly DateOnly Today = new(2026, 7, 8);
    private static readonly DateOnly Start = new(2025, 1, 1);

    private static readonly DayRecord Workout = new(1, 0, 45, null);
    private static readonly DayRecord Walk = new(0, 1, 30, null);
    private static DayRecord StepsOnly(int steps) => new(0, 0, 0, steps);

    private static Dictionary<DayOfWeek, ScheduleSlot> Schedule(params DayOfWeek[] days) =>
        days.ToDictionary(d => d, _ => new ScheduleSlot(null, null));

    private static ConsistencyInput Input(
        Dictionary<DayOfWeek, ScheduleSlot>? schedule = null,
        Dictionary<DateOnly, DayRecord>? days = null,
        DateOnly? trackingStart = null,
        DateOnly? scheduleSince = null,
        int stepGoal = 8000) =>
        new(Today, stepGoal, schedule ?? [], days ?? [], trackingStart ?? Start, scheduleSince ?? Start);

    private static Dictionary<DateOnly, DayRecord> On(params (DateOnly Date, DayRecord Record)[] entries) =>
        entries.ToDictionary(e => e.Date, e => e.Record);

    private static readonly DayOfWeek[] MonWedFri = [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday];

    /// <summary>Done on every Mon/Wed/Fri from <paramref name="from"/> up to and including <paramref name="to"/>.</summary>
    private static Dictionary<DateOnly, DayRecord> EveryScheduledDay(DateOnly from, DateOnly to, params DateOnly[] except)
    {
        var days = new Dictionary<DateOnly, DayRecord>();
        for (var d = from; d <= to; d = d.AddDays(1))
            if (MonWedFri.Contains(d.DayOfWeek) && !except.Contains(d)) days[d] = Workout;
        return days;
    }

    // --- counting a day ---

    [Fact]
    public void A_day_counts_workouts_activities_and_a_met_step_goal()
    {
        Assert.Equal(0, ConsistencyCalculator.Sessions(null, 8000));
        Assert.Equal(1, ConsistencyCalculator.Sessions(Workout, 8000));
        Assert.Equal(3, ConsistencyCalculator.Sessions(new DayRecord(1, 1, 75, 9000), 8000));
        Assert.Equal(0, ConsistencyCalculator.Sessions(StepsOnly(7999), 8000));
        Assert.Equal(1, ConsistencyCalculator.Sessions(StepsOnly(8000), 8000));
    }

    [Fact]
    public void The_heat_level_is_the_session_count_up_to_four()
    {
        Assert.Equal(0, ConsistencyCalculator.Level(0));
        Assert.Equal(2, ConsistencyCalculator.Level(2));
        Assert.Equal(4, ConsistencyCalculator.Level(4));
        Assert.Equal(4, ConsistencyCalculator.Level(9));
    }

    [Theory]
    [InlineData(2026, 7, 6, 2026, 7, 6)] // Monday is its own week start
    [InlineData(2026, 7, 8, 2026, 7, 6)]
    [InlineData(2026, 7, 12, 2026, 7, 6)] // Sunday belongs to the week that began the Monday before
    [InlineData(2026, 7, 13, 2026, 7, 13)]
    public void Weeks_start_on_monday(int y, int m, int d, int ey, int em, int ed) =>
        Assert.Equal(new DateOnly(ey, em, ed), ConsistencyCalculator.WeekStart(new DateOnly(y, m, d)));

    // --- this week ---

    [Fact]
    public void The_week_strip_shows_each_day_against_the_plan()
    {
        var schedule = Schedule(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday, DayOfWeek.Sunday);
        var days = On((new DateOnly(2026, 7, 6), Workout), (new DateOnly(2026, 7, 7), Walk));

        var week = ConsistencyCalculator.Compute(Input(schedule, days)).Week;

        Assert.Equal(7, week.Count);
        Assert.Equal(new DateOnly(2026, 7, 6), week[0].Date);
        Assert.Equal(
            [DayState.Done, DayState.Bonus, DayState.Pending, DayState.Rest, DayState.Upcoming, DayState.Rest, DayState.Upcoming],
            week.Select(d => d.State));
        Assert.Equal([false, false, true, false, false, false, false], week.Select(d => d.IsToday));
    }

    [Fact]
    public void A_scheduled_day_with_nothing_logged_is_missed_once_it_has_passed()
    {
        var week = ConsistencyCalculator.Compute(Input(Schedule(DayOfWeek.Monday, DayOfWeek.Wednesday))).Week;

        Assert.Equal(DayState.Missed, week[0].State);
        Assert.Equal(DayState.Pending, week[2].State); // today isn't missed, it just isn't done yet
    }

    [Fact]
    public void Doing_something_today_completes_a_scheduled_day()
    {
        var days = On((Today, StepsOnly(9500)));
        var week = ConsistencyCalculator.Compute(Input(Schedule(DayOfWeek.Wednesday), days)).Week;

        Assert.Equal(DayState.Done, week[2].State);
        Assert.Equal(9500, week[2].Steps);
    }

    [Fact]
    public void Days_before_the_schedule_existed_are_not_called_missed()
    {
        var input = Input(Schedule(DayOfWeek.Monday, DayOfWeek.Wednesday), scheduleSince: new DateOnly(2026, 7, 7));
        var week = ConsistencyCalculator.Compute(input).Week;

        Assert.Equal(DayState.Rest, week[0].State); // Monday was before the schedule was set
    }

    [Fact]
    public void The_week_strip_carries_the_planned_template()
    {
        var id = Guid.NewGuid();
        var schedule = new Dictionary<DayOfWeek, ScheduleSlot> { [DayOfWeek.Wednesday] = new(id, "Leg day") };

        var today = ConsistencyCalculator.Compute(Input(schedule)).Week[2];

        Assert.Equal(id, today.TemplateId);
        Assert.Equal("Leg day", today.TemplateName);
    }

    // --- weekly adherence ---

    [Fact]
    public void Weeks_where_everything_scheduled_was_done_score_one_hundred_percent()
    {
        var days = EveryScheduledDay(new DateOnly(2026, 5, 1), Today);
        var weeks = ConsistencyCalculator.Compute(Input(Schedule(MonWedFri), days)).Weeks;

        Assert.Equal(12, weeks.Count);
        Assert.Equal(Today.AddDays(-2), weeks[^1].WeekStart); // the current week is last
        Assert.All(weeks.Skip(5), w => Assert.Equal(100, w.Percent));
        Assert.Equal(3, weeks[^2].Due);
    }

    [Fact]
    public void The_current_week_only_counts_days_that_have_happened()
    {
        var days = On((new DateOnly(2026, 7, 6), Workout));
        var current = ConsistencyCalculator.Compute(Input(Schedule(MonWedFri), days)).Weeks[^1];

        Assert.Equal(3, current.Scheduled); // Mon, Wed, Fri this week
        Assert.Equal(1, current.Due); // only Monday is behind us (Wednesday, today, isn't done yet)
        Assert.Equal(1, current.Completed);
        Assert.Equal(100, current.Percent);
    }

    [Fact]
    public void A_week_with_misses_scores_the_share_done()
    {
        var days = On((new DateOnly(2026, 6, 29), Workout)); // the Monday of the previous week only
        var previous = ConsistencyCalculator.Compute(Input(Schedule(MonWedFri), days)).Weeks[^2];

        Assert.Equal(3, previous.Due);
        Assert.Equal(1, previous.Completed);
        Assert.Equal(33, previous.Percent);
    }

    [Fact]
    public void Weeks_before_tracking_began_are_not_judged()
    {
        var input = Input(Schedule(MonWedFri), trackingStart: new DateOnly(2026, 6, 29));
        var weeks = ConsistencyCalculator.Compute(input).Weeks;

        Assert.All(weeks.Take(10), w => Assert.Null(w.Percent));
        Assert.Equal(0, weeks[0].Due);
        Assert.Equal(3, weeks[^2].Due);
    }

    [Fact]
    public void A_schedule_set_part_way_through_a_week_only_counts_from_then()
    {
        // Set up on Wednesday the 1st: of the previous week's Mon/Wed/Fri, only Wed and Fri count.
        var input = Input(Schedule(MonWedFri), scheduleSince: new DateOnly(2026, 7, 1));
        var previous = ConsistencyCalculator.Compute(input).Weeks[^2];

        Assert.Equal(2, previous.Scheduled);
        Assert.Equal(2, previous.Due);
    }

    [Fact]
    public void Extra_days_show_up_as_active_days_but_do_not_change_adherence()
    {
        var days = On((new DateOnly(2026, 7, 7), Walk), (new DateOnly(2026, 7, 6), Workout));
        var current = ConsistencyCalculator.Compute(Input(Schedule(MonWedFri), days)).Weeks[^1];

        Assert.Equal(2, current.ActiveDays);
        Assert.Equal(1, current.Due);
    }

    // --- streaks ---

    [Fact]
    public void An_unbroken_run_of_scheduled_days_is_the_streak()
    {
        var from = new DateOnly(2026, 6, 15);
        var input = Input(Schedule(MonWedFri), EveryScheduledDay(from, Today), trackingStart: from, scheduleSince: from);

        var streak = ConsistencyCalculator.Compute(input).Summary.Streak;

        Assert.Equal(11, streak.Current);
        Assert.Equal(11, streak.Best);
    }

    [Fact]
    public void A_missed_scheduled_day_ends_the_streak_but_keeps_the_best()
    {
        var from = new DateOnly(2026, 6, 15);
        var days = EveryScheduledDay(from, Today, except: new DateOnly(2026, 7, 3));
        var streak = ConsistencyCalculator.Compute(Input(Schedule(MonWedFri), days, from, from)).Summary.Streak;

        Assert.Equal(2, streak.Current); // Mon 6th and today
        Assert.Equal(8, streak.Best); // 15th through 1st
    }

    [Fact]
    public void Today_not_being_done_yet_does_not_break_the_streak()
    {
        var from = new DateOnly(2026, 6, 15);
        var days = EveryScheduledDay(from, Today.AddDays(-1));
        var streak = ConsistencyCalculator.Compute(Input(Schedule(MonWedFri), days, from, from)).Summary.Streak;

        Assert.Equal(10, streak.Current);
    }

    [Fact]
    public void Training_on_a_rest_day_does_not_affect_the_streak()
    {
        var from = new DateOnly(2026, 6, 15);
        var days = EveryScheduledDay(from, Today);
        days[new DateOnly(2026, 6, 16)] = Walk; // a Tuesday
        var streak = ConsistencyCalculator.Compute(Input(Schedule(MonWedFri), days, from, from)).Summary.Streak;

        Assert.Equal(11, streak.Current);
    }

    [Fact]
    public void Nothing_is_judged_without_a_schedule()
    {
        var days = On((new DateOnly(2026, 7, 6), Workout), (Today, Walk));
        var result = ConsistencyCalculator.Compute(Input(days: days));

        Assert.False(result.HasSchedule);
        Assert.All(result.Week, d => Assert.True(d.State is DayState.Rest or DayState.Bonus));
        Assert.All(result.Weeks, w => Assert.Null(w.Percent));
        Assert.Equal(0, result.Summary.Streak.Current);
        Assert.Null(result.Summary.AdherencePercent4Weeks);
        Assert.Equal(2, result.Summary.ActiveDays30); // still counts what was done
    }

    // --- summary ---

    [Fact]
    public void Active_days_are_counted_over_each_window()
    {
        var days = On((Today, Workout), (Today.AddDays(-29), Walk), (Today.AddDays(-30), Walk), (Today.AddDays(-100), Workout), (Today.AddDays(-400), Workout));
        var summary = ConsistencyCalculator.Compute(Input(days: days)).Summary;

        Assert.Equal(2, summary.ActiveDays30);
        Assert.Equal(3, summary.ActiveDays90);
        Assert.Equal(4, summary.ActiveDays365);
    }

    [Fact]
    public void Several_sessions_on_one_day_are_still_one_active_day()
    {
        var days = On((Today, new DayRecord(2, 1, 120, 12000)));
        Assert.Equal(1, ConsistencyCalculator.Compute(Input(days: days)).Summary.ActiveDays30);
    }

    [Fact]
    public void Four_week_adherence_is_the_share_of_due_days_done()
    {
        // The 28 days to Wed 8 Jul start on Thu 11 Jun. Mon/Wed/Fri days in it that have passed (today is not done,
        // so it isn't due): 12 Jun, 15, 17, 19, 22, 24, 26, 29 Jun, 1, 3, 6 Jul = 11.
        // Done: every one from 17 Jun on except the 24th and 6 Jul (7 days). 12 and 15 Jun were never logged.
        var days = EveryScheduledDay(new DateOnly(2026, 6, 17), Today.AddDays(-1), except: [new(2026, 6, 24), new(2026, 7, 6)]);

        var summary = ConsistencyCalculator.Compute(Input(Schedule(MonWedFri), days)).Summary;

        Assert.Equal(7, days.Count);
        Assert.Equal(64, summary.AdherencePercent4Weeks); // 7 of 11
    }

    // --- the heatmap ---

    [Fact]
    public void The_heatmap_covers_fifty_three_weeks_ending_this_week()
    {
        var result = ConsistencyCalculator.Compute(Input());

        Assert.Equal(DayOfWeek.Monday, result.HeatmapStart.DayOfWeek);
        Assert.Equal(ConsistencyCalculator.WeekStart(Today).AddDays(-7 * 52), result.HeatmapStart);
    }

    [Fact]
    public void The_heatmap_lists_only_days_with_something_in_range()
    {
        var start = ConsistencyCalculator.Compute(Input()).HeatmapStart;
        var days = On(
            (Today, new DayRecord(1, 1, 75, null)),
            (start, Walk),
            (start.AddDays(-1), Workout), // just outside the range
            (Today.AddDays(-3), StepsOnly(5000))); // under the goal: shown, but level 0

        var cells = ConsistencyCalculator.Compute(Input(days: days)).Heatmap;

        Assert.Equal(3, cells.Count);
        Assert.Equal(2, cells.Single(c => c.Date == Today).Level);
        Assert.Equal(1, cells.Single(c => c.Date == start).Level);
        var lowSteps = cells.Single(c => c.Date == Today.AddDays(-3));
        Assert.Equal(0, lowSteps.Level);
        Assert.Equal(5000, lowSteps.Steps);
        Assert.Equal(cells.OrderBy(c => c.Date), cells);
    }

    [Fact]
    public void Meeting_the_step_goal_lights_up_a_day_and_missing_it_does_not()
    {
        var days = On((Today.AddDays(-1), StepsOnly(8000)), (Today.AddDays(-2), StepsOnly(7999)));
        var result = ConsistencyCalculator.Compute(Input(days: days));

        Assert.Equal(1, result.Heatmap.Single(c => c.Date == Today.AddDays(-1)).Level);
        Assert.Equal(0, result.Heatmap.Single(c => c.Date == Today.AddDays(-2)).Level);
        Assert.Equal(1, result.Summary.ActiveDays30);
    }
}
