namespace OpenIron.Api.Data.Entities;

/// <summary>A weekday the user plans to train on, and optionally which template they plan to do.</summary>
public class ScheduledDay
{
    public Guid UserId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>Cleared if the template is deleted; the day stays scheduled.</summary>
    public Guid? WorkoutTemplateId { get; set; }
}

/// <summary>Per-user consistency settings. One row per user, created the first time they save a schedule.</summary>
public class ConsistencySettings
{
    public const int DefaultStepGoal = 8000;

    public Guid UserId { get; set; }
    public int StepGoal { get; set; } = DefaultStepGoal;

    /// <summary>
    /// When the set of scheduled days last changed. Days before this aren't judged against the schedule, so
    /// setting one up doesn't mark the past month as missed.
    /// </summary>
    public DateOnly ScheduleSince { get; set; }
}
