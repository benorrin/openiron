using OpenIron.Api.Contracts;

namespace OpenIron.Api.Services;

/// <summary>What happened on one day, already counted up.</summary>
public record DayRecord(int Workouts, int Activities, int Minutes, int? Steps);

public record ScheduleSlot(Guid? TemplateId, string? TemplateName);

public record ConsistencyInput(
    DateOnly Today,
    int StepGoal,
    IReadOnlyDictionary<DayOfWeek, ScheduleSlot> Schedule,
    IReadOnlyDictionary<DateOnly, DayRecord> Days,
    /// <summary>The first day anything could have been logged (the account's start, or earlier data).</summary>
    DateOnly TrackingStart,
    /// <summary>When the set of scheduled days last changed.</summary>
    DateOnly ScheduleSince);

/// <summary>
/// Matches what someone actually did against the days they planned to train.
///
/// A day is active if it has a lifting workout, a logged activity, or steps at or over the goal. A scheduled day is
/// done if it is active: the plan's template is a prompt, not a requirement. Days before the schedule was set up (or
/// before the account existed) are never judged, and today only counts once it has been done, so an unfinished
/// day never counts against anyone.
/// </summary>
public static class ConsistencyCalculator
{
    public const int HeatmapWeeks = 53;
    public const int AdherenceWeeks = 12;
    public const int MaxLevel = 4;

    /// <summary>How many separate things count on a day: workouts, activities, and a met step goal.</summary>
    public static int Sessions(DayRecord? day, int stepGoal) =>
        day is null ? 0 : day.Workouts + day.Activities + (day.Steps >= stepGoal ? 1 : 0);

    public static int Level(int sessions) => Math.Min(sessions, MaxLevel);

    /// <summary>Weeks run Monday to Sunday.</summary>
    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static ConsistencyDto Compute(ConsistencyInput input)
    {
        var today = input.Today;
        var judgedFrom = input.TrackingStart > input.ScheduleSince ? input.TrackingStart : input.ScheduleSince;

        DayRecord? Record(DateOnly d) => input.Days.GetValueOrDefault(d);
        int SessionsOn(DateOnly d) => Sessions(Record(d), input.StepGoal);
        bool Active(DateOnly d) => SessionsOn(d) > 0;
        bool Scheduled(DateOnly d) => input.Schedule.ContainsKey(d.DayOfWeek);

        // A scheduled day we can fairly judge: it has happened, or it is today and already done.
        bool Due(DateOnly d) => Scheduled(d) && d >= judgedFrom && (d < today || (d == today && Active(d)));

        return new ConsistencyDto(
            today,
            input.StepGoal,
            input.Schedule.Count > 0,
            WeekStrip(input, judgedFrom, SessionsOn, Active, Scheduled),
            Weeks(today, judgedFrom, input, Scheduled, Due, Active, SessionsOn),
            Summary(input, judgedFrom, Due, Active),
            WeekStart(today).AddDays(-7 * (HeatmapWeeks - 1)),
            Heatmap(input, SessionsOn));
    }

    private static List<WeekDayDto> WeekStrip(
        ConsistencyInput input, DateOnly judgedFrom, Func<DateOnly, int> sessionsOn, Func<DateOnly, bool> active, Func<DateOnly, bool> scheduled)
    {
        var start = WeekStart(input.Today);
        return Enumerable.Range(0, 7).Select(i =>
        {
            var date = start.AddDays(i);
            var slot = input.Schedule.GetValueOrDefault(date.DayOfWeek);
            var isActive = active(date);

            DayState state;
            if (!scheduled(date)) state = isActive ? DayState.Bonus : DayState.Rest;
            else if (isActive) state = DayState.Done;
            else if (date > input.Today) state = DayState.Upcoming;
            else if (date == input.Today) state = DayState.Pending;
            else state = date >= judgedFrom ? DayState.Missed : DayState.Rest;

            return new WeekDayDto(
                date, date.DayOfWeek, date == input.Today, state, slot?.TemplateId, slot?.TemplateName,
                sessionsOn(date), input.Days.GetValueOrDefault(date)?.Steps);
        }).ToList();
    }

    private static List<WeekSummaryDto> Weeks(
        DateOnly today, DateOnly judgedFrom, ConsistencyInput input,
        Func<DateOnly, bool> scheduled, Func<DateOnly, bool> due, Func<DateOnly, bool> active, Func<DateOnly, int> sessionsOn)
    {
        var current = WeekStart(today);
        return Enumerable.Range(0, AdherenceWeeks).Select(i =>
        {
            var weekStart = current.AddDays(-7 * (AdherenceWeeks - 1 - i));
            var days = Enumerable.Range(0, 7).Select(n => weekStart.AddDays(n)).ToList();

            var planned = days.Count(d => scheduled(d) && d >= judgedFrom);
            var dueDays = days.Where(due).ToList();
            var completed = dueDays.Count(active);
            var activeDays = days.Count(d => d <= today && sessionsOn(d) > 0);

            return new WeekSummaryDto(
                weekStart, planned, dueDays.Count, completed, activeDays,
                dueDays.Count == 0 ? null : (int)Math.Round(100.0 * completed / dueDays.Count));
        }).ToList();
    }

    private static ConsistencySummaryDto Summary(
        ConsistencyInput input, DateOnly judgedFrom, Func<DateOnly, bool> due, Func<DateOnly, bool> active)
    {
        var today = input.Today;
        int ActiveDays(int days) => Enumerable.Range(0, days).Count(i => active(today.AddDays(-i)));

        var recentDue = Enumerable.Range(0, 28).Select(i => today.AddDays(-i)).Where(due).ToList();
        int? adherence = recentDue.Count == 0 ? null : (int)Math.Round(100.0 * recentDue.Count(active) / recentDue.Count);

        return new ConsistencySummaryDto(ActiveDays(30), ActiveDays(90), ActiveDays(365), adherence, Streaks(input, judgedFrom, active));
    }

    /// <summary>
    /// Runs of scheduled days that were all done, in a row. Rest days in between don't break a run; a missed
    /// scheduled day does. Today only breaks nothing: if it isn't done yet it is simply left out.
    /// </summary>
    private static StreakDto Streaks(ConsistencyInput input, DateOnly judgedFrom, Func<DateOnly, bool> active)
    {
        var from = input.Today.AddDays(-365);
        if (judgedFrom > from) from = judgedFrom;

        var best = 0;
        var run = 0;
        for (var d = from; d <= input.Today; d = d.AddDays(1))
        {
            if (!input.Schedule.ContainsKey(d.DayOfWeek)) continue;
            if (d == input.Today && !active(d)) continue;

            run = active(d) ? run + 1 : 0;
            best = Math.Max(best, run);
        }

        // Whatever run is still going when we reach today is the current streak.
        return new StreakDto(run, best);
    }

    private static List<HeatCellDto> Heatmap(ConsistencyInput input, Func<DateOnly, int> sessionsOn)
    {
        var start = WeekStart(input.Today).AddDays(-7 * (HeatmapWeeks - 1));
        var cells = new List<HeatCellDto>();
        for (var d = start; d <= input.Today; d = d.AddDays(1))
        {
            if (!input.Days.TryGetValue(d, out var record)) continue;
            var sessions = sessionsOn(d);
            if (sessions == 0 && record.Minutes == 0 && (record.Steps ?? 0) == 0) continue;

            cells.Add(new HeatCellDto(d, Level(sessions), record.Workouts, record.Activities, record.Minutes, record.Steps));
        }
        return cells;
    }
}
