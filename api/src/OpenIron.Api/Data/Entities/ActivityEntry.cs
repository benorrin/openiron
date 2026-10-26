using System.Text.Json.Serialization;

namespace OpenIron.Api.Data.Entities;

[JsonConverter(typeof(CamelEnumConverter<ActivityType>))]
public enum ActivityType
{
    Walk,
    Run,
    Treadmill,
    Cycling,
    Swim,
    Hike,
    Other,
    /// <summary>A day's step count, rather than a session. There is at most one per day.</summary>
    Steps,
}

/// <summary>Something the user did that isn't a logged lifting workout: a walk, a run, a day's steps.</summary>
public class ActivityEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public ActivityType Type { get; set; }

    /// <summary>Required for every type except <see cref="ActivityType.Steps"/>.</summary>
    public int? DurationMinutes { get; set; }
    public double? DistanceKm { get; set; }

    /// <summary>Required for, and only used by, <see cref="ActivityType.Steps"/>.</summary>
    public int? Steps { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
