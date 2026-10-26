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

public record InviteDto(
    Guid Id,
    string? Note,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    string Status,
    string? UsedBy,
    DateTime? UsedAt);

public record CreateInviteRequest([StringLength(100)] string? Note, [Range(1, 365)] int? ExpiresInDays);

/// <summary>The only response that ever contains the code itself.</summary>
public record CreatedInviteDto(InviteDto Invite, string Code);
