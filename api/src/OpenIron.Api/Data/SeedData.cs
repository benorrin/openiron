using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Data.Entities;

namespace OpenIron.Api.Data;

/// <summary>The built-in exercise library every user can pick from.</summary>
public static class SeedData
{
    private static readonly (string Name, string MuscleGroup)[] Library =
    [
        ("Barbell Back Squat", "Legs"),
        ("Front Squat", "Legs"),
        ("Leg Press", "Legs"),
        ("Romanian Deadlift", "Legs"),
        ("Walking Lunge", "Legs"),
        ("Leg Extension", "Legs"),
        ("Leg Curl", "Legs"),
        ("Standing Calf Raise", "Legs"),
        ("Deadlift", "Back"),
        ("Barbell Row", "Back"),
        ("Pull-Up", "Back"),
        ("Lat Pulldown", "Back"),
        ("Seated Cable Row", "Back"),
        ("Dumbbell Row", "Back"),
        ("Bench Press", "Chest"),
        ("Incline Bench Press", "Chest"),
        ("Dumbbell Bench Press", "Chest"),
        ("Chest Fly", "Chest"),
        ("Push-Up", "Chest"),
        ("Dip", "Chest"),
        ("Overhead Press", "Shoulders"),
        ("Dumbbell Shoulder Press", "Shoulders"),
        ("Lateral Raise", "Shoulders"),
        ("Face Pull", "Shoulders"),
        ("Barbell Curl", "Arms"),
        ("Dumbbell Curl", "Arms"),
        ("Hammer Curl", "Arms"),
        ("Triceps Pushdown", "Arms"),
        ("Skull Crusher", "Arms"),
        ("Plank", "Core"),
        ("Hanging Leg Raise", "Core"),
        ("Cable Crunch", "Core"),
    ];

    public static async Task EnsureAsync(AppDbContext db)
    {
        if (await db.Exercises.AnyAsync(e => e.UserId == null)) return;

        db.Exercises.AddRange(Library.Select(x => new Exercise { Name = x.Name, MuscleGroup = x.MuscleGroup }));
        await db.SaveChangesAsync();
    }
}
