using OpenIron.Api.Data.Entities;

namespace OpenIron.Api.Data;

public static class ExerciseQueries
{
    /// <summary>Built-in exercises plus the user's own custom ones.</summary>
    public static IQueryable<Exercise> VisibleTo(this IQueryable<Exercise> exercises, Guid userId) =>
        exercises.Where(e => e.UserId == null || e.UserId == userId);
}
