using System.ComponentModel.DataAnnotations;

namespace OpenIron.Api.Contracts;

public record AdminUserDto(
    Guid Id,
    string Username,
    DateTime CreatedAt,
    bool IsAdmin,
    bool IsDisabled,
    bool ShareActivity,
    bool IsYou,
    int WorkoutCount,
    DateTime? LastWorkoutAt);

/// <summary>Only the fields that are present are changed.</summary>
public record AdminUserUpdate(bool? IsAdmin, bool? IsDisabled);

public record AdminResetPasswordRequest([Required, MinLength(8), MaxLength(128)] string NewPassword);

public record AdminSettingsDto(bool RegistrationOpen);

public record AdminOverviewDto(
    int TotalUsers,
    int AdminCount,
    int DisabledUsers,
    int TotalWorkouts,
    int WorkoutsLast30Days,
    bool RegistrationOpen);
