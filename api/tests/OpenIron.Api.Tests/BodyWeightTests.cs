using System.Net;
using System.Net.Http.Json;
using OpenIron.Api.Contracts;

namespace OpenIron.Api.Tests;

public class BodyWeightTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Entries_are_returned_oldest_first_and_filterable_by_date()
    {
        var client = await factory.CreateAuthenticatedAsync();
        foreach (var (day, kg) in new[] { (10, 81.5), (1, 82.0), (20, 80.8) })
            (await client.PostAsJsonAsync("/api/body-weight", new BodyWeightRequest(new DateOnly(2026, 3, day), kg)))
                .EnsureSuccessStatusCode();

        var all = (await client.GetFromJsonAsync<List<BodyWeightDto>>("/api/body-weight"))!;
        Assert.Equal([82.0, 81.5, 80.8], all.Select(e => e.WeightKg));

        var filtered = (await client.GetFromJsonAsync<List<BodyWeightDto>>("/api/body-weight?from=2026-03-05&to=2026-03-15"))!;
        Assert.Equal([81.5], filtered.Select(e => e.WeightKg));
    }

    [Fact]
    public async Task Logging_the_same_day_twice_replaces_the_entry()
    {
        var client = await factory.CreateAuthenticatedAsync();
        var day = new DateOnly(2026, 4, 2);

        var first = await client.PostAsJsonAsync("/api/body-weight", new BodyWeightRequest(day, 80));
        var second = await client.PostAsJsonAsync("/api/body-weight", new BodyWeightRequest(day, 79.4));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var entry = Assert.Single((await client.GetFromJsonAsync<List<BodyWeightDto>>("/api/body-weight"))!);
        Assert.Equal(79.4, entry.WeightKg);
    }

    [Fact]
    public async Task Entries_are_private_and_deletable_and_validated()
    {
        var alice = await factory.CreateAuthenticatedAsync();
        var bob = await factory.CreateAuthenticatedAsync();
        var entry = (await (await alice.PostAsJsonAsync("/api/body-weight",
            new BodyWeightRequest(new DateOnly(2026, 4, 2), 80))).Content.ReadFromJsonAsync<BodyWeightDto>())!;

        Assert.Empty((await bob.GetFromJsonAsync<List<BodyWeightDto>>("/api/body-weight"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/body-weight/{entry.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await alice.PostAsJsonAsync("/api/body-weight", new BodyWeightRequest(new DateOnly(2026, 4, 3), 5))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/api/body-weight/{entry.Id}")).StatusCode);
    }
}
