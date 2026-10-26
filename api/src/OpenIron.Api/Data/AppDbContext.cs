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
    public DbSet<ServerSettings> ServerSettings => Set<ServerSettings>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<NutritionSettings> NutritionSettings => Set<NutritionSettings>();
    public DbSet<ActivityEntry> Activities => Set<ActivityEntry>();
    public DbSet<ScheduledDay> ScheduledDays => Set<ScheduledDay>();
    public DbSet<ConsistencySettings> ConsistencySettings => Set<ConsistencySettings>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.NormalizedUsername).IsUnique();
            e.Property(u => u.Username).HasMaxLength(32);
            e.Property(u => u.NormalizedUsername).HasMaxLength(32);
        });

        b.Entity<ServerSettings>(e => e.Property(x => x.Id).ValueGeneratedNever());

        b.Entity<Invite>(e =>
        {
            e.Property(x => x.CodeHash).HasMaxLength(64);
            e.Property(x => x.Note).HasMaxLength(100);
            e.HasIndex(x => x.CodeHash).IsUnique();
            // Invites outlive the people they mention: deleting an account keeps the record, minus the link.
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UsedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<NutritionSettings>(e =>
        {
            e.HasKey(x => x.UserId);
            // Stored as text so the table stays readable and reordering an enum can't change what a row means.
            e.Property(x => x.Sex).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Activity).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Goal).HasConversion<string>().HasMaxLength(20);
            e.HasOne<User>().WithOne().HasForeignKey<NutritionSettings>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ActivityEntry>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => new { x.UserId, x.Date });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ScheduledDay>(e =>
        {
            e.HasKey(x => new { x.UserId, x.DayOfWeek });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<WorkoutTemplate>().WithMany().HasForeignKey(x => x.WorkoutTemplateId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<ConsistencySettings>(e =>
        {
            e.HasKey(x => x.UserId);
            e.HasOne<User>().WithOne().HasForeignKey<ConsistencySettings>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
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
