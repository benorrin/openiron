using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Contracts;
using OpenIron.Api.Data.Entities;

namespace OpenIron.Api.Tests;

public class NutritionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    // A 30-year-old, 180 cm man, moderately active, wanting to lose 5 kg at half a kilo a week.
    private static NutritionSettingsRequest Settings(
        GoalType goal = GoalType.Lose, double? target = 75, double rate = 0.5, DateOnly? dob = null) =>
        new(Sex.Male, dob ?? Today.AddYears(-30), 180, ActivityLevel.Moderate, goal, target, rate);

    private static Task<HttpResponseMessage> Save(HttpClient c, NutritionSettingsRequest s) => c.PutAsJsonAsync("/api/nutrition/settings", s);

    private static Task LogWeight(HttpClient c, double kg, int daysAgo = 0) =>
        c.PostAsJsonAsync("/api/body-weight", new BodyWeightRequest(Today.AddDays(-daysAgo), kg));

    private static async Task<NutritionDto> Get(HttpClient c) => (await c.GetFromJsonAsync<NutritionDto>("/api/nutrition"))!;

    [Fact]
    public async Task A_new_user_has_nothing_set_up()
    {
        var client = await factory.CreateAuthenticatedAsync("nut_new");
        var result = await Get(client);

        Assert.Null(result.Settings);
        Assert.Null(result.Plan);
        Assert.Equal("needsSettings", result.PlanUnavailableReason);
    }

    [Fact]
    public async Task Settings_without_a_weigh_in_wait_for_one()
    {
        var client = await factory.CreateAuthenticatedAsync("nut_waiting");
        var saved = await Save(client, Settings());
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var result = await Get(client);
        Assert.NotNull(result.Settings);
        Assert.Null(result.Plan);
        Assert.Equal("needsWeighIn", result.PlanUnavailableReason);
    }

    [Fact]
    public async Task A_weigh_in_produces_a_plan_from_the_formula()
    {
        var client = await factory.CreateAuthenticatedAsync("nut_plan");
        await Save(client, Settings());
        await LogWeight(client, 80);

        var plan = (await Get(client)).Plan!;

        Assert.Equal(30, plan.Age);
        Assert.Equal(1780, plan.BmrKcal);
        Assert.Equal(2759, plan.FormulaMaintenanceKcal);
        Assert.Equal(2210, plan.TargetKcal);
        Assert.Equal(ProgressStatus.NoPlan, plan.Progress.Status);
        Assert.Equal(-5, plan.Progress.RemainingKg);
        Assert.NotNull(plan.Progress.EstimatedGoalDate);
        Assert.Equal(160, plan.Macros.ProteinG);
    }

    [Fact]
    public async Task Enums_travel_as_readable_camel_case_strings()
    {
        var client = await factory.CreateAuthenticatedAsync("nut_json");
        await client.PutAsJsonAsync("/api/nutrition/settings", new
        {
            sex = "female", dateOfBirth = Today.AddYears(-25).ToString("yyyy-MM-dd"), heightCm = 165,
            activity = "veryActive", goal = "gain", targetWeightKg = 65, weeklyRateKg = 0.25,
        });

        var json = await (await client.GetAsync("/api/nutrition")).Content.ReadAsStringAsync();
        Assert.Contains("\"activity\":\"veryActive\"", json);
        Assert.Contains("\"sex\":\"female\"", json);
        Assert.Contains("\"goal\":\"gain\"", json);

        var bad = await client.PutAsJsonAsync("/api/nutrition/settings", new
        {
            sex = "robot", dateOfBirth = "1990-01-01", heightCm = 170, activity = "light", goal = "lose", targetWeightKg = 70, weeklyRateKg = 0.5,
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var numeric = await client.PutAsJsonAsync("/api/nutrition/settings", new
        {
            sex = 0, dateOfBirth = "1990-01-01", heightCm = 170, activity = "light", goal = "lose", targetWeightKg = 70, weeklyRateKg = 0.5,
        });
        Assert.Equal(HttpStatusCode.BadRequest, numeric.StatusCode);
    }

    [Fact]
    public async Task Invalid_settings_are_rejected()
    {
        var client = await factory.CreateAuthenticatedAsync("nut_invalid");

        Assert.Equal(HttpStatusCode.BadRequest, (await Save(client, Settings(dob: Today.AddYears(-17)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(client, Settings(dob: Today.AddYears(-101)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(client, Settings(dob: Today.AddYears(1)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(client, Settings(target: null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(client, Settings(rate: 0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(client, Settings(rate: 2))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(client, Settings() with { HeightCm = 50 })).StatusCode);
        Assert.Null((await Get(client)).Settings);
    }

    [Fact]
    public async Task Maintaining_ignores_any_target_and_rate()
    {
        var client = await factory.CreateAuthenticatedAsync("nut_maintain");
        await Save(client, Settings(GoalType.Maintain, target: 70, rate: 0.7));

        var settings = (await Get(client)).Settings!;
        Assert.Null(settings.TargetWeightKg);
        Assert.Equal(0, settings.WeeklyRateKg);
    }

    [Fact]
    public async Task Applying_a_target_records_it_and_starts_the_check()
    {
        var client = await factory.CreateAuthenticatedAsync("nut_apply");
        await Save(client, Settings());
        await LogWeight(client, 80);

        var response = await client.PostAsJsonAsync("/api/nutrition/apply", new ApplyTargetRequest(null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<NutritionDto>())!;

        Assert.Equal(2210, result.Settings!.AppliedKcal);
        Assert.Equal(Today, result.Settings.AppliedOn);
        Assert.Equal(ProgressStatus.Gathering, result.Plan!.Progress.Status);
        Assert.Equal(19, result.Plan.Progress.DaysUntilCheck);
    }

    [Fact]
    public async Task Users_can_apply_their_own_number_instead()
    {
        var client = await factory.CreateAuthenticatedAsync("nut_own");
        await Save(client, Settings());
        await LogWeight(client, 80);

        var result = (await (await client.PostAsJsonAsync("/api/nutrition/apply", new ApplyTargetRequest(2400))).Content.ReadFromJsonAsync<NutritionDto>())!;
        Assert.Equal(2400, result.Settings!.AppliedKcal);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/nutrition/apply", new ApplyTargetRequest(100))).StatusCode);
    }

    [Fact]
    public async Task Applying_needs_a_profile_and_a_weight_to_recommend_from()
    {
        var client = await factory.CreateAuthenticatedAsync("nut_early");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/nutrition/apply", new ApplyTargetRequest(null))).StatusCode);

        await Save(client, Settings());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/nutrition/apply", new ApplyTargetRequest(null))).StatusCode);
    }

    [Fact]
    public async Task The_applied_target_can_be_cleared()
    {
        var client = await factory.CreateAuthenticatedAsync("nut_clear");
        await Save(client, Settings());
        await LogWeight(client, 80);
        await client.PostAsJsonAsync("/api/nutrition/apply", new ApplyTargetRequest(null));

        var cleared = (await (await client.DeleteAsync("/api/nutrition/apply")).Content.ReadFromJsonAsync<NutritionDto>())!;

        Assert.Null(cleared.Settings!.AppliedKcal);
        Assert.Equal(ProgressStatus.NoPlan, cleared.Plan!.Progress.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await (await factory.CreateAuthenticatedAsync("nut_clear2")).DeleteAsync("/api/nutrition/apply")).StatusCode);
    }

    [Fact]
    public async Task Changing_the_goal_keeps_what_the_user_has_been_eating()
    {
        var client = await factory.CreateAuthenticatedAsync("nut_goal");
        await Save(client, Settings());
        await LogWeight(client, 80);
        await client.PostAsJsonAsync("/api/nutrition/apply", new ApplyTargetRequest(null));

        var changed = (await (await Save(client, Settings(GoalType.Gain, 85, 0.25))).Content.ReadFromJsonAsync<NutritionDto>())!;

        Assert.Equal(2210, changed.Settings!.AppliedKcal);
        Assert.Equal(GoalType.Gain, changed.Settings.Goal);
        Assert.Equal(0.25, changed.Plan!.Progress.PlannedRateKgPerWeek);
    }

    [Fact]
    public async Task A_target_followed_with_slow_progress_is_flagged_with_an_adjustment()
    {
        var client = await factory.CreateAuthenticatedAsync("nut_slow");
        await Save(client, Settings());
        // Weigh-ins every day for 40 days, losing 0.1 kg a week, against a plan that assumed half a kilo.
        for (var i = 40; i >= 0; i--) await LogWeight(client, 80 + 0.1 * i / 7.0, i);
        await client.PostAsJsonAsync("/api/nutrition/apply", new ApplyTargetRequest(2210));
        var userId = (await client.GetFromJsonAsync<UserDto>("/api/auth/me"))!.Id;
        await factory.WithDbAsync(async db =>
        {
            // As if the target had been applied 40 days ago, which the API has no way to do directly.
            (await db.NutritionSettings.SingleAsync(s => s.UserId == userId)).AppliedOn = Today.AddDays(-40);
            await db.SaveChangesAsync();
        });

        var plan = (await Get(client)).Plan!;

        Assert.Equal(ProgressStatus.SlowerThanPlanned, plan.Progress.Status);
        Assert.InRange(plan.Progress.ObservedRateKgPerWeek!.Value, -0.12, -0.08);
        Assert.True(plan.Progress.SuggestedAdjustmentKcal < -300);
        Assert.True(plan.TargetKcal < 2210);
    }

    [Fact]
    public async Task Nutrition_data_is_private_to_the_user()
    {
        var alice = await factory.CreateAuthenticatedAsync("nut_alice");
        var bob = await factory.CreateAuthenticatedAsync("nut_bob");
        await Save(alice, Settings());
        await LogWeight(alice, 80);
        await alice.PutAsJsonAsync("/api/community/settings", new CommunitySettingsDto(true));

        Assert.Null((await Get(bob)).Settings);

        var profile = await (await bob.GetAsync("/api/community/users/nut_alice")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("kcal", profile, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("calorie", profile, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deleting_an_account_removes_its_nutrition_settings()
    {
        var client = await factory.CreateAuthenticatedAsync("nut_leaver");
        await Save(client, Settings());
        await factory.WithDbAsync(async db => Assert.True(await db.NutritionSettings.AnyAsync(s => db.Users.Any(u => u.Id == s.UserId && u.Username == "nut_leaver"))));

        await client.PostAsJsonAsync("/api/account/delete", new DeleteAccountRequest("correct horse battery"));

        await factory.WithDbAsync(async db => Assert.False(await db.NutritionSettings.AnyAsync(s => !db.Users.Any(u => u.Id == s.UserId))));
    }

    [Fact]
    public async Task Nutrition_needs_a_login()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/nutrition")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PutAsJsonAsync("/api/nutrition/settings", Settings())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/nutrition/apply", new ApplyTargetRequest(null))).StatusCode);
    }
}
