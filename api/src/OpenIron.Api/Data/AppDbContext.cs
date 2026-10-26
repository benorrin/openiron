using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Data.Entities;

namespace OpenIron.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<WorkoutTemplate> WorkoutTemplates => Set<WorkoutTemplate>();
    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<BodyWeightEntry> BodyWeightEntries => Set<BodyWeightEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.NormalizedUsername).IsUnique();
            e.Property(u => u.Username).HasMaxLength(32);
            e.Property(u => u.NormalizedUsername).HasMaxLength(32);
        });

        b.Entity<Exercise>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.MuscleGroup).HasMaxLength(50);
            e.HasIndex(x => x.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WorkoutTemplate>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => x.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Exercises).WithOne().HasForeignKey(x => x.WorkoutTemplateId).OnDelete(DeleteBehavior.Cascade);
        });

        // Exercises in use by a template or logged workout can't be deleted out from under it.
        b.Entity<TemplateExercise>(e =>
            e.HasOne(x => x.Exercise).WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict));

        b.Entity<Workout>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => new { x.UserId, x.PerformedAt });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<WorkoutTemplate>().WithMany().HasForeignKey(x => x.WorkoutTemplateId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Exercises).WithOne().HasForeignKey(x => x.WorkoutId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WorkoutExercise>(e =>
        {
            e.HasOne(x => x.Exercise).WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Sets).WithOne().HasForeignKey(x => x.WorkoutExerciseId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<BodyWeightEntry>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Date }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
