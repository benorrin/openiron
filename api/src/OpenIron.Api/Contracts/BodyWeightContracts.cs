using System.ComponentModel.DataAnnotations;

namespace OpenIron.Api.Contracts;

public record BodyWeightDto(Guid Id, DateOnly Date, double WeightKg);

public record BodyWeightRequest(DateOnly Date, [Range(20, 500)] double WeightKg);
