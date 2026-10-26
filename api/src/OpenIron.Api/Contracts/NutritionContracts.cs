using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using OpenIron.Api.Data.Entities;

namespace OpenIron.Api.Contracts;

[JsonConverter(typeof(CamelEnumConverter<ProgressStatus>))]
public enum ProgressStatus
{
    /// <summary>No target has been applied yet, so there is nothing to judge progress against.</summary>
    NoPlan,
    /// <summary>A target is being followed but there isn't enough data since it started to judge it.</summary>
    Gathering,
    OnTrack,
    FasterThanPlanned,
    SlowerThanPlanned,
    /// <summary>Weight is moving the opposite way to the goal.</summary>
    WrongDirection,
    /// <summary>Maintaining, but weight is creeping up.</summary>
    DriftingUp,
    /// <summary>Maintaining, but weight is creeping down.</summary>
    DriftingDown,
}

public record NutritionSettingsDto(
    Sex Sex,
    DateOnly DateOfBirth,
    double HeightCm,
    ActivityLevel Activity,
    GoalType Goal,
    double? TargetWeightKg,
    double WeeklyRateKg,
    int? AppliedKcal,
    DateOnly? AppliedOn);

public record NutritionSettingsRequest(
    Sex Sex,
    DateOnly DateOfBirth,
    [Range(120, 230)] double HeightCm,
    ActivityLevel Activity,
    GoalType Goal,
    [Range(30, 300)] double? TargetWeightKg,
    [Range(0, 1)] double WeeklyRateKg);

public record ApplyTargetRequest([Range(800, 8000)] int? Calories);

public record MacrosDto(int ProteinG, int FatG, int CarbsG);

public record TrendPointDto(DateOnly Date, double WeightKg);

public record ProgressDto(
    ProgressStatus Status,
    double? ObservedRateKgPerWeek,
    double PlannedRateKgPerWeek,
    int WeighInsUsed,
    int DaysOfData,
    int? DaysUntilCheck,
    int? SuggestedAdjustmentKcal,
    double? RemainingKg,
    bool GoalReached,
    DateOnly? EstimatedGoalDate);

public record PlanDto(
    double CurrentWeightKg,
    int Age,
    int BmrKcal,
    int FormulaMaintenanceKcal,
    int EstimatedMaintenanceKcal,
    double Confidence,
    int TargetKcal,
    bool LimitedBySafetyFloor,
    bool RateLimited,
    MacrosDto Macros,
    ProgressDto Progress,
    List<TrendPointDto> Trend);

/// <summary>Everything the nutrition page needs. Plan is null until settings exist and there is a weigh-in.</summary>
public record NutritionDto(NutritionSettingsDto? Settings, PlanDto? Plan, string? PlanUnavailableReason);
