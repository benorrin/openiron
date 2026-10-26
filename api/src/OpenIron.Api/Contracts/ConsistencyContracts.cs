using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using OpenIron.Api.Data.Entities;

namespace OpenIron.Api.Contracts;

public record ActivityDto(
    Guid Id,
    DateOnly Date,
    ActivityType Type,
    int? DurationMinutes,
    double? DistanceKm,
    int? Steps,
    string? Notes);

public record ActivityRequest(
    DateOnly Date,
    ActivityType Type,
    [Range(1, 1440)] int? DurationMinutes,
    [Range(0, 1000)] double? DistanceKm,
    [Range(1, 200_000)] int? Steps,
    [StringLength(500)] string? Notes);

public record ScheduledDayDto(DayOfWeek DayOfWeek, Guid? TemplateId, string? TemplateName);

public record ScheduledDayRequest(DayOfWeek DayOfWeek, Guid? TemplateId);

public record ScheduleDto(List<ScheduledDayDto> Days, int StepGoal, DateOnly? Since);

public record ScheduleRequest(
    [Required, MaxLength(7)] List<ScheduledDayRequest> Days,
    [Range(1000, 100_000)] int StepGoal);

[JsonConverter(typeof(CamelEnumConverter<DayState>))]
public enum DayState
{
    /// <summary>Scheduled and done.</summary>
    Done,
    /// <summary>Scheduled, in the past, and nothing was logged.</summary>
    Missed,
    /// <summary>Scheduled for today and not done yet.</summary>
    Pending,
    /// <summary>Scheduled for later this week.</summary>
    Upcoming,
    /// <summary>Not scheduled, and nothing logged.</summary>
    Rest,
    /// <summary>Not scheduled, but they trained anyway.</summary>
    Bonus,
}

public record WeekDayDto(
    DateOnly Date,
    DayOfWeek DayOfWeek,
    bool IsToday,
    DayState State,
    Guid? TemplateId,
    string? TemplateName,
    int Sessions,
    int? Steps);

public record WeekSummaryDto(DateOnly WeekStart, int Scheduled, int Due, int Completed, int ActiveDays, int? Percent);

public record HeatCellDto(DateOnly Date, int Level, int Workouts, int Activities, int Minutes, int? Steps);

public record StreakDto(int Current, int Best);

public record ConsistencySummaryDto(int ActiveDays30, int ActiveDays90, int ActiveDays365, int? AdherencePercent4Weeks, StreakDto Streak);

public record ConsistencyDto(
    DateOnly Today,
    int StepGoal,
    bool HasSchedule,
    List<WeekDayDto> Week,
    List<WeekSummaryDto> Weeks,
    ConsistencySummaryDto Summary,
    DateOnly HeatmapStart,
    List<HeatCellDto> Heatmap);
