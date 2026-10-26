namespace OpenIron.Api.Data.Entities;

public class Exercise
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Owner of a custom exercise; null for the built-in library shared by everyone.</summary>
    public Guid? UserId { get; set; }
    public required string Name { get; set; }
    public required string MuscleGroup { get; set; }
}
