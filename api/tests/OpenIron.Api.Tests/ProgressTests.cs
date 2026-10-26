using System.Net;
using System.Net.Http.Json;
using OpenIron.Api.Contracts;

namespace OpenIron.Api.Tests;

public class ProgressTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<ExerciseDto> Exercise(HttpClient client, string name) =>
        (await client.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises"))!.First(e => e.Name == name);

    private static Task<HttpResponseMessage> Log(HttpClient client, ExerciseDto exercise, DateTime when, params (int Reps, double Kg)[] sets) =>
        client.PostAsJsonAsync("/api/workouts", new WorkoutRequest("Session", when, null, null, null,
            [new(exercise.Id, sets.Select(s => new SetRequest(s.Reps, s.Kg)).ToList())]));

    [Fact]
    public async Task Exercise_progress_has_one_point_per_session_oldest_first_with_records()
    {
        var client = await factory.CreateAuthenticatedAsync();
        var squat = await Exercise(client, "Barbell Back Squat");
        var bench = await Exercise(client, "Bench Press");
        var start = DateTime.UtcNow.AddDays(-30);

        await Log(client, squat, start.AddDays(14), (5, 110), (5, 110));
        await Log(client, squat, start, (5, 100));
        await Log(client, bench, start.AddDays(2), (5, 60));

        var progress = (await client.GetFromJsonAsync<ExerciseProgressDto>($"/api/progress/exercises/{squat.Id}"))!;

        Assert.Equal([100, 110], progress.Points.Select(p => p.TopWeightKg));
        Assert.Equal(1100, progress.Points[1].VolumeKg);
        Assert.Equal(110, progress.Records.HeaviestWeightKg);
        Assert.Equal(progress.Points[1].PerformedAt, progress.Records.HeaviestOn);
    }

    [Fact]
    public async Task Progress_only_covers_the_callers_own_workouts()
    {
        var alice = await factory.CreateAuthenticatedAsync();
        var bob = await factory.CreateAuthenticatedAsync();
        var squat = await Exercise(alice, "Barbell Back Squat");
        await Log(alice, squat, DateTime.UtcNow, (5, 100));

        var bobs = (await bob.GetFromJsonAsync<ExerciseProgressDto>($"/api/progress/exercises/{squat.Id}"))!;
        Assert.Empty(bobs.Points);
        Assert.Empty((await bob.GetFromJsonAsync<List<ProgressExerciseDto>>("/api/progress/exercises"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/progress/exercises/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Logged_exercise_list_is_most_recent_first()
    {
        var client = await factory.CreateAuthenticatedAsync();
        var squat = await Exercise(client, "Barbell Back Squat");
        var bench = await Exercise(client, "Bench Press");
        await Log(client, squat, DateTime.UtcNow.AddDays(-5), (5, 100));
        await Log(client, squat, DateTime.UtcNow.AddDays(-4), (5, 100));
        await Log(client, bench, DateTime.UtcNow.AddDays(-1), (5, 60));

        var list = (await client.GetFromJsonAsync<List<ProgressExerciseDto>>("/api/progress/exercises"))!;
        Assert.Equal(["Bench Press", "Barbell Back Squat"], list.Select(e => e.Name));
        Assert.Equal(2, list[1].Sessions);
    }

    [Fact]
    public async Task Overview_summarises_activity_and_body_weight()
    {
        var client = await factory.CreateAuthenticatedAsync();
        var squat = await Exercise(client, "Barbell Back Squat");
        await Log(client, squat, DateTime.UtcNow.AddHours(-2), (5, 100));
        await Log(client, squat, DateTime.UtcNow.AddDays(-90), (5, 100));
        await client.PostAsJsonAsync("/api/body-weight", new BodyWeightRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)), 82));
        await client.PostAsJsonAsync("/api/body-weight", new BodyWeightRequest(DateOnly.FromDateTime(DateTime.UtcNow), 80.5));

        var overview = (await client.GetFromJsonAsync<OverviewDto>("/api/progress/overview?weeks=4"))!;

        Assert.Equal(2, overview.TotalWorkouts);
        Assert.Equal(1, overview.WorkoutsLast30Days);
        Assert.Equal(80.5, overview.LatestBodyWeightKg);
        Assert.Equal(-1.5, overview.BodyWeightChangeKg);
        Assert.Equal(4, overview.Weeks.Count);
        Assert.Equal(1, overview.Weeks[^1].Workouts);
        Assert.Equal(500, overview.Weeks[^1].VolumeKg);
    }

    [Fact]
    public async Task Overview_for_a_new_user_is_empty()
    {
        var client = await factory.CreateAuthenticatedAsync();
        var overview = (await client.GetFromJsonAsync<OverviewDto>("/api/progress/overview"))!;

        Assert.Equal(0, overview.TotalWorkouts);
        Assert.Null(overview.LatestBodyWeightKg);
        Assert.Null(overview.BodyWeightChangeKg);
        Assert.Equal(12, overview.Weeks.Count);
    }
}
