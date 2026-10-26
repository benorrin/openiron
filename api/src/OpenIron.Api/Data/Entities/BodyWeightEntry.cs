namespace OpenIron.Api.Data.Entities;

public class BodyWeightEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public double WeightKg { get; set; }
}
