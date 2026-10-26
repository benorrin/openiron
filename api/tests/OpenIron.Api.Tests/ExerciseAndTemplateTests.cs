using System.Net;
using System.Net.Http.Json;
using OpenIron.Api.Contracts;

namespace OpenIron.Api.Tests;

public class ExerciseAndTemplateTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Exercise_library_is_seeded()
    {
        var client = await factory.CreateAuthenticatedAsync();
        var exercises = await client.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises");
        Assert.Contains(exercises!, e => e.Name == "Bench Press" && !e.IsCustom);
    }

    [Fact]
    public async Task Custom_exercises_are_private_and_builtins_are_read_only()
    {
        var alice = await factory.CreateAuthenticatedAsync();
        var bob = await factory.CreateAuthenticatedAsync();

        var created = await alice.PostAsJsonAsync("/api/exercises", new ExerciseRequest("Zercher Squat", "Legs"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var custom = (await created.Content.ReadFromJsonAsync<ExerciseDto>())!;
        Assert.True(custom.IsCustom);

        Assert.DoesNotContain((await bob.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises"))!, e => e.Id == custom.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/exercises/{custom.Id}")).StatusCode);

        var builtin = (await alice.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises"))!.First(e => !e.IsCustom);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.DeleteAsync($"/api/exercises/{builtin.Id}")).StatusCode);

        var renamed = await alice.PutAsJsonAsync($"/api/exercises/{custom.Id}", new ExerciseRequest("Zercher", "Legs"));
        Assert.Equal("Zercher", (await renamed.Content.ReadFromJsonAsync<ExerciseDto>())!.Name);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/api/exercises/{custom.Id}")).StatusCode);
    }

    [Fact]
    public async Task Template_crud_round_trip_keeps_exercise_order()
    {
        var client = await factory.CreateAuthenticatedAsync();
        var lib = (await client.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises"))!;
        var squat = lib.First(e => e.Name == "Barbell Back Squat");
        var rdl = lib.First(e => e.Name == "Romanian Deadlift");

        var response = await client.PostAsJsonAsync("/api/templates", new TemplateRequest("Leg day", "Heavy", [
            new(squat.Id, 5, 5),
            new(rdl.Id, 3, 8),
        ]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var template = (await response.Content.ReadFromJsonAsync<TemplateDto>())!;
        Assert.Equal(["Barbell Back Squat", "Romanian Deadlift"], template.Exercises.Select(e => e.ExerciseName));

        var updated = await client.PutAsJsonAsync($"/api/templates/{template.Id}", new TemplateRequest("Leg day B", null, [
            new(rdl.Id, 4, 6),
            new(squat.Id, 3, 10),
        ]));
        updated.EnsureSuccessStatusCode();
        var reloaded = (await client.GetFromJsonAsync<TemplateDto>($"/api/templates/{template.Id}"))!;
        Assert.Equal("Leg day B", reloaded.Name);
        Assert.Null(reloaded.Notes);
        Assert.Equal(["Romanian Deadlift", "Barbell Back Squat"], reloaded.Exercises.Select(e => e.ExerciseName));
        Assert.Equal(4, reloaded.Exercises[0].TargetSets);

        Assert.Single((await client.GetFromJsonAsync<List<TemplateDto>>("/api/templates"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/templates/{template.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/templates/{template.Id}")).StatusCode);
    }

    [Fact]
    public async Task Templates_belong_to_their_owner_and_reject_foreign_exercises()
    {
        var alice = await factory.CreateAuthenticatedAsync();
        var bob = await factory.CreateAuthenticatedAsync();
        var squat = (await alice.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises"))!.First();

        var template = (await (await alice.PostAsJsonAsync("/api/templates",
            new TemplateRequest("Mine", null, [new(squat.Id, 3, 5)]))).Content.ReadFromJsonAsync<TemplateDto>())!;

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/templates/{template.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/templates/{template.Id}")).StatusCode);

        var custom = (await (await alice.PostAsJsonAsync("/api/exercises", new ExerciseRequest("Secret lift", "Back")))
            .Content.ReadFromJsonAsync<ExerciseDto>())!;
        var stolen = await bob.PostAsJsonAsync("/api/templates", new TemplateRequest("Bobs", null, [new(custom.Id, 3, 5)]));
        Assert.Equal(HttpStatusCode.BadRequest, stolen.StatusCode);
    }

    [Fact]
    public async Task Template_requires_at_least_one_exercise_and_sane_targets()
    {
        var client = await factory.CreateAuthenticatedAsync();
        var squat = (await client.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises"))!.First();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/templates", new TemplateRequest("Empty", null, []))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/templates", new TemplateRequest("Bad", null, [new(squat.Id, 0, 5)]))).StatusCode);
    }

    [Fact]
    public async Task Exercise_in_use_by_a_template_cannot_be_deleted()
    {
        var client = await factory.CreateAuthenticatedAsync();
        var custom = (await (await client.PostAsJsonAsync("/api/exercises", new ExerciseRequest("Odd lift", "Core")))
            .Content.ReadFromJsonAsync<ExerciseDto>())!;
        await client.PostAsJsonAsync("/api/templates", new TemplateRequest("T", null, [new(custom.Id, 3, 5)]));

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/exercises/{custom.Id}")).StatusCode);
    }
}
