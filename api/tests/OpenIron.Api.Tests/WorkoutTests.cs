using System.Net;
using System.Net.Http.Json;
using OpenIron.Api.Contracts;

namespace OpenIron.Api.Tests;

public class WorkoutTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<ExerciseDto> Exercise(HttpClient client, string name) =>
        (await client.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises"))!.First(e => e.Name == name);

    private static WorkoutRequest Session(Guid exerciseId, DateTime when, params (int Reps, double Kg)[] sets) =>
        new("Leg day", when, 60, null, null, [new(exerciseId, sets.Select(s => new SetRequest(s.Reps, s.Kg)).ToList())]);

    [Fact]
    public async Task Logging_a_workout_computes_volume_and_lists_it()
    {
        var client = await factory.CreateAuthenticatedAsync();
        var squat = await Exercise(client, "Barbell Back Squat");

        var response = await client.PostAsJsonAsync("/api/workouts",
            Session(squat.Id, new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc), (5, 100), (5, 100), (3, 110)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var workout = (await response.Content.ReadFromJsonAsync<WorkoutDto>())!;

        Assert.Equal(5 * 100 + 5 * 100 + 3 * 110, workout.TotalVolumeKg);
        Assert.Equal([100, 100, 110], workout.Exercises[0].Sets.Select(s => s.WeightKg));

        var summary = Assert.Single((await client.GetFromJsonAsync<List<WorkoutSummaryDto>>("/api/workouts"))!);
        Assert.Equal(3, summary.SetCount);
        Assert.Equal(1330, summary.TotalVolumeKg);
    }

    [Fact]
    public async Task Workouts_list_newest_first()
    {
        var client = await factory.CreateAuthenticatedAsync();
        var squat = await Exercise(client, "Barbell Back Squat");
        foreach (var day in new[] { 5, 20, 12 })
            await client.PostAsJsonAsync("/api/workouts",
                Session(squat.Id, new DateTime(2026, 3, day, 9, 0, 0, DateTimeKind.Utc), (5, 100)));

        var list = (await client.GetFromJsonAsync<List<WorkoutSummaryDto>>("/api/workouts"))!;
        Assert.Equal([20, 12, 5], list.Select(w => w.PerformedAt.Day));
    }

    [Fact]
    public async Task Updating_a_workout_replaces_its_exercises_and_sets()
    {
        var client = await factory.CreateAuthenticatedAsync();
        var squat = await Exercise(client, "Barbell Back Squat");
        var bench = await Exercise(client, "Bench Press");
        var created = (await (await client.PostAsJsonAsync("/api/workouts",
            Session(squat.Id, DateTime.UtcNow, (5, 100), (5, 100)))).Content.ReadFromJsonAsync<WorkoutDto>())!;

        var update = Session(bench.Id, created.PerformedAt, (8, 60)) with { Name = "Push" };
        var updated = await client.PutAsJsonAsync($"/api/workouts/{created.Id}", update);
        updated.EnsureSuccessStatusCode();

        var reloaded = (await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{created.Id}"))!;
        Assert.Equal("Push", reloaded.Name);
        var exercise = Assert.Single(reloaded.Exercises);
        Assert.Equal("Bench Press", exercise.ExerciseName);
        Assert.Equal(480, reloaded.TotalVolumeKg);
    }

    [Fact]
    public async Task Workouts_are_private_to_their_owner()
    {
        var alice = await factory.CreateAuthenticatedAsync();
        var bob = await factory.CreateAuthenticatedAsync();
        var squat = await Exercise(alice, "Barbell Back Squat");
        var workout = (await (await alice.PostAsJsonAsync("/api/workouts",
            Session(squat.Id, DateTime.UtcNow, (5, 100)))).Content.ReadFromJsonAsync<WorkoutDto>())!;

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/workouts/{workout.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await bob.PutAsJsonAsync($"/api/workouts/{workout.Id}", Session(squat.Id, DateTime.UtcNow, (1, 1)))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/workouts/{workout.Id}")).StatusCode);
        Assert.Empty((await bob.GetFromJsonAsync<List<WorkoutSummaryDto>>("/api/workouts"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/api/workouts/{workout.Id}")).StatusCode);
    }

    [Fact]
    public async Task Workout_validates_sets()
    {
        var client = await factory.CreateAuthenticatedAsync();
        var squat = await Exercise(client, "Barbell Back Squat");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/workouts", Session(squat.Id, DateTime.UtcNow, (0, 100)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/workouts", Session(squat.Id, DateTime.UtcNow, (5, -1)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/workouts", Session(squat.Id, DateTime.UtcNow))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/workouts", Session(Guid.NewGuid(), DateTime.UtcNow, (5, 100)))).StatusCode);
    }

    [Fact]
    public async Task Draft_from_template_prefills_last_used_weights()
    {
        var client = await factory.CreateAuthenticatedAsync();
        var squat = await Exercise(client, "Barbell Back Squat");
        var bench = await Exercise(client, "Bench Press");
        var template = (await (await client.PostAsJsonAsync("/api/templates", new TemplateRequest("Mixed", null,
            [new(squat.Id, 3, 5), new(bench.Id, 2, 8)]))).Content.ReadFromJsonAsync<TemplateDto>())!;

        var first = (await client.GetFromJsonAsync<WorkoutDraftDto>($"/api/workouts/draft/{template.Id}"))!;
        Assert.Equal(3, first.Exercises[0].Sets.Count);
        Assert.All(first.Exercises[0].Sets, s => Assert.Equal((5, 0), (s.Reps, s.WeightKg)));

        await client.PostAsJsonAsync("/api/workouts", Session(squat.Id, DateTime.UtcNow.AddDays(-7), (5, 90), (5, 95)));
        await client.PostAsJsonAsync("/api/workouts", Session(squat.Id, DateTime.UtcNow.AddDays(-1), (5, 100), (5, 105)));

        var second = (await client.GetFromJsonAsync<WorkoutDraftDto>($"/api/workouts/draft/{template.Id}"))!;
        // Most recent session wins; sets beyond what was logged repeat the final weight.
        Assert.Equal([100, 105, 105], second.Exercises[0].Sets.Select(s => s.WeightKg));
        Assert.Equal([0, 0], second.Exercises[1].Sets.Select(s => s.WeightKg));
    }

    [Fact]
    public async Task Deleting_a_template_keeps_workouts_logged_from_it()
    {
        var client = await factory.CreateAuthenticatedAsync();
        var squat = await Exercise(client, "Barbell Back Squat");
        var template = (await (await client.PostAsJsonAsync("/api/templates", new TemplateRequest("T", null,
            [new(squat.Id, 3, 5)]))).Content.ReadFromJsonAsync<TemplateDto>())!;
        var workout = (await (await client.PostAsJsonAsync("/api/workouts",
            Session(squat.Id, DateTime.UtcNow, (5, 100)) with { TemplateId = template.Id })).Content.ReadFromJsonAsync<WorkoutDto>())!;
        Assert.Equal(template.Id, workout.TemplateId);

        await client.DeleteAsync($"/api/templates/{template.Id}");
        var after = (await client.GetFromJsonAsync<WorkoutDto>($"/api/workouts/{workout.Id}"))!;
        Assert.Null(after.TemplateId);
    }
}
