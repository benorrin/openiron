using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenIron.Api.Data.Entities;

/// <summary>Serialises enums as camelCase strings ("veryActive") and refuses numbers, so the API stays readable.</summary>
public class CamelEnumConverter<T>() : JsonStringEnumConverter<T>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    where T : struct, Enum;

[JsonConverter(typeof(CamelEnumConverter<Sex>))]
public enum Sex
{
    Male,
    Female,
    /// <summary>Only affects the BMR formula, which has a different constant for each; this uses the midpoint.</summary>
    Unspecified,
}

[JsonConverter(typeof(CamelEnumConverter<ActivityLevel>))]
public enum ActivityLevel
{
    Sedentary,
    Light,
    Moderate,
    Active,
    VeryActive,
}

[JsonConverter(typeof(CamelEnumConverter<GoalType>))]
public enum GoalType
{
    Lose,
    Maintain,
    Gain,
}

/// <summary>A user's profile, goal, and the calorie target they are currently following. One row per user.</summary>
public class NutritionSettings
{
    public Guid UserId { get; set; }
    public Sex Sex { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public double HeightCm { get; set; }
    public ActivityLevel Activity { get; set; }
    public GoalType Goal { get; set; }
    public double? TargetWeightKg { get; set; }

    /// <summary>How fast they want to change, as a positive number of kg per week; the direction comes from <see cref="Goal"/>.</summary>
    public double WeeklyRateKg { get; set; }

    /// <summary>The calories the user has been aiming for since <see cref="AppliedOn"/>. The adaptive estimate assumes they ate this.</summary>
    public int? AppliedKcal { get; set; }
    public DateOnly? AppliedOn { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
