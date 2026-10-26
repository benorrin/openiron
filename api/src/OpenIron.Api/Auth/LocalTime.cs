namespace OpenIron.Api.Auth;

/// <summary>
/// Works out a user's local calendar day. The server only knows UTC, so the browser says how far its clock is from
/// UTC (the value of JavaScript's <c>getTimezoneOffset()</c>: minutes to add to local time to get UTC, so UTC+1 is -60).
/// Sending it per request, rather than storing a zone, copes with travel and daylight saving.
/// </summary>
public static class LocalTime
{
    public const int MaxOffsetMinutes = 14 * 60;

    public static bool IsValid(int offsetMinutes) => Math.Abs(offsetMinutes) <= MaxOffsetMinutes;

    public static DateTime ToLocal(DateTime utc, int offsetMinutes) => utc.AddMinutes(-offsetMinutes);

    public static DateOnly DateOf(DateTime utc, int offsetMinutes) => DateOnly.FromDateTime(ToLocal(utc, offsetMinutes));

    public static DateOnly Today(int offsetMinutes) => DateOf(DateTime.UtcNow, offsetMinutes);
}
