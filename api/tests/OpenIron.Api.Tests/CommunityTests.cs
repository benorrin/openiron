using System.Net;
using System.Net.Http.Json;
using OpenIron.Api.Contracts;
using OpenIron.Api.Services;

namespace OpenIron.Api.Tests;

public class CommunityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static Task<HttpResponseMessage> Share(HttpClient client, bool share) =>
        client.PutAsJsonAsync("/api/community/settings", new CommunitySettingsDto(share));

    private static async Task LogSquat(HttpClient client, DateTime when, params (int Reps, double Kg)[] sets)
    {
        var squat = (await client.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises"))!.First(e => e.Name == "Barbell Back Squat");
        (await client.PostAsJsonAsync("/api/workouts", new WorkoutRequest("Legs", when, null, null, null,
            [new(squat.Id, sets.Select(s => new SetRequest(s.Reps, s.Kg)).ToList())]))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Activity_is_private_until_the_user_opts_in()
    {
        var alice = await factory.CreateAuthenticatedAsync("alice_c1");
        var bob = await factory.CreateAuthenticatedAsync("bob_c1");
        await LogSquat(alice, DateTime.UtcNow, (5, 100));

        Assert.False((await alice.GetFromJsonAsync<CommunitySettingsDto>("/api/community/settings"))!.ShareActivity);
        Assert.DoesNotContain((await bob.GetFromJsonAsync<List<CommunityMemberDto>>("/api/community/users"))!, m => m.Username == "alice_c1");
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync("/api/community/users/alice_c1")).StatusCode);
    }

    [Fact]
    public async Task Not_sharing_looks_the_same_as_not_existing()
    {
        await factory.CreateAuthenticatedAsync("alice_c2");
        var bob = await factory.CreateAuthenticatedAsync("bob_c2");

        var private_ = await bob.GetAsync("/api/community/users/alice_c2");
        var missing = await bob.GetAsync("/api/community/users/nobody_here");

        Assert.Equal(HttpStatusCode.NotFound, private_.StatusCode);
        Assert.Equal(missing.StatusCode, private_.StatusCode);
        // Bodies carry a per-request trace id, so compare the parts that could reveal an account.
        Assert.Equal(missing.Content.Headers.ContentLength > 0, private_.Content.Headers.ContentLength > 0);
        Assert.Equal(missing.Content.Headers.ContentType, private_.Content.Headers.ContentType);
    }

    [Fact]
    public async Task Opting_in_shares_general_progress_with_other_users()
    {
        var alice = await factory.CreateAuthenticatedAsync("alice_c3");
        var bob = await factory.CreateAuthenticatedAsync("bob_c3");
        await LogSquat(alice, DateTime.UtcNow.AddDays(-2), (5, 100), (5, 100));
        await LogSquat(alice, DateTime.UtcNow.AddDays(-60), (3, 120));
        await alice.PostAsJsonAsync("/api/body-weight", new BodyWeightRequest(DateOnly.FromDateTime(DateTime.UtcNow), 80));
        (await Share(alice, true)).EnsureSuccessStatusCode();

        var member = Assert.Single((await bob.GetFromJsonAsync<List<CommunityMemberDto>>("/api/community/users"))!, m => m.Username == "alice_c3");
        Assert.False(member.IsYou);
        Assert.Equal(2, member.TotalWorkouts);
        Assert.Equal(1, member.WorkoutsLast30Days);
        Assert.Equal(1000, member.VolumeLast30DaysKg);

        var profile = (await bob.GetFromJsonAsync<CommunityProfileDto>("/api/community/users/ALICE_c3"))!;
        Assert.Equal(12, profile.Weeks.Count);
        Assert.Equal(2, profile.RecentWorkouts.Count);
        Assert.Equal(["Barbell Back Squat"], profile.TopLifts.Select(l => l.ExerciseName));
        Assert.Equal(120, profile.TopLifts[0].HeaviestWeightKg);
        Assert.Equal(2, profile.TopLifts[0].Sessions);
    }

    [Fact]
    public async Task Body_weight_is_never_part_of_the_shared_profile()
    {
        var alice = await factory.CreateAuthenticatedAsync("alice_c4");
        var bob = await factory.CreateAuthenticatedAsync("bob_c4");
        await alice.PostAsJsonAsync("/api/body-weight", new BodyWeightRequest(DateOnly.FromDateTime(DateTime.UtcNow), 87.654));
        await Share(alice, true);

        var json = await (await bob.GetAsync("/api/community/users/alice_c4")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("87.654", json);
        Assert.DoesNotContain("bodyWeight", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Opting_out_hides_the_user_again()
    {
        var alice = await factory.CreateAuthenticatedAsync("alice_c5");
        var bob = await factory.CreateAuthenticatedAsync("bob_c5");
        await Share(alice, true);
        Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync("/api/community/users/alice_c5")).StatusCode);

        await Share(alice, false);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync("/api/community/users/alice_c5")).StatusCode);
        Assert.DoesNotContain((await bob.GetFromJsonAsync<List<CommunityMemberDto>>("/api/community/users"))!, m => m.Username == "alice_c5");
    }

    [Fact]
    public async Task You_can_always_view_your_own_profile()
    {
        var alice = await factory.CreateAuthenticatedAsync("alice_c6");
        await LogSquat(alice, DateTime.UtcNow, (5, 100));

        var profile = (await alice.GetFromJsonAsync<CommunityProfileDto>("/api/community/users/alice_c6"))!;
        Assert.True(profile.Member.IsYou);
        Assert.Equal(1, profile.Member.TotalWorkouts);
    }

    [Fact]
    public async Task Members_are_ordered_by_recent_activity()
    {
        var quiet = await factory.CreateAuthenticatedAsync("quiet_c7");
        var busy = await factory.CreateAuthenticatedAsync("busy_c7");
        await LogSquat(quiet, DateTime.UtcNow.AddDays(-1), (5, 100));
        await LogSquat(busy, DateTime.UtcNow.AddDays(-1), (5, 100));
        await LogSquat(busy, DateTime.UtcNow.AddDays(-2), (5, 100));
        await Share(quiet, true);
        await Share(busy, true);

        var names = (await quiet.GetFromJsonAsync<List<CommunityMemberDto>>("/api/community/users"))!
            .Select(m => m.Username).Where(n => n.EndsWith("_c7")).ToList();
        Assert.Equal(["busy_c7", "quiet_c7"], names);
    }

    [Fact]
    public async Task Community_endpoints_require_a_login()
    {
        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/community/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/community/users/anyone")).StatusCode);
    }

    [Fact]
    public void Summarise_counts_the_last_30_days_and_finds_the_latest_workout()
    {
        var now = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        var id = Guid.NewGuid();
        var summary = ProgressCalculator.Summarise([
            new WorkoutVolume(id, now.AddDays(-1), 500),
            new WorkoutVolume(id, now.AddDays(-29), 300),
            new WorkoutVolume(id, now.AddDays(-45), 900),
        ], now);

        Assert.Equal(3, summary.TotalWorkouts);
        Assert.Equal(2, summary.WorkoutsLast30Days);
        Assert.Equal(800, summary.VolumeLast30DaysKg);
        Assert.Equal(now.AddDays(-1), summary.LastWorkoutAt);
        Assert.Null(ProgressCalculator.Summarise([], now).LastWorkoutAt);
    }
}
