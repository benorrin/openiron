using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Contracts;
using OpenIron.Api.Data.Entities;

namespace OpenIron.Api.Tests;

public class ConsistencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static ActivityRequest Walk(DateOnly? date = null, int minutes = 30) =>
        new(date ?? Today, ActivityType.Walk, minutes, null, null, null);

    private static ActivityRequest Steps(int steps, DateOnly? date = null) =>
        new(date ?? Today, ActivityType.Steps, null, null, steps, null);

    private static async Task<ConsistencyDto> Consistency(HttpClient c, int tz = 0) =>
        (await c.GetFromJsonAsync<ConsistencyDto>($"/api/consistency?tz={tz}"))!;

    private static async Task LogWorkout(HttpClient client, DateTime when)
    {
        var squat = (await client.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises"))!.First();
        (await client.PostAsJsonAsync("/api/workouts", new WorkoutRequest("Session", when, null, null, null,
            [new(squat.Id, [new SetRequest(5, 100)])]))).EnsureSuccessStatusCode();
    }

    // --- activities ---

    [Fact]
    public async Task Activities_can_be_logged_listed_and_deleted()
    {
        var client = await factory.CreateAuthenticatedAsync("con_basic");
        var created = await client.PostAsJsonAsync("/api/activities",
            new ActivityRequest(Today, ActivityType.Treadmill, 40, 5.5, null, "easy pace"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var entry = (await created.Content.ReadFromJsonAsync<ActivityDto>())!;
        Assert.Equal((ActivityType.Treadmill, 40, 5.5), (entry.Type, entry.DurationMinutes, entry.DistanceKm));

        var list = (await client.GetFromJsonAsync<List<ActivityDto>>("/api/activities"))!;
        Assert.Equal(entry.Id, Assert.Single(list).Id);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/activities/{entry.Id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ActivityDto>>("/api/activities"))!);
    }

    [Fact]
    public async Task Activity_types_are_camel_case_strings()
    {
        var client = await factory.CreateAuthenticatedAsync("con_json");
        await client.PostAsJsonAsync("/api/activities", Walk());

        var json = await (await client.GetAsync("/api/activities")).Content.ReadAsStringAsync();
        Assert.Contains("\"type\":\"walk\"", json);
    }

    [Fact]
    public async Task Sessions_need_a_duration_and_steps_need_a_count()
    {
        var client = await factory.CreateAuthenticatedAsync("con_validate");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/activities", new ActivityRequest(Today, ActivityType.Run, null, 5, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/activities", new ActivityRequest(Today, ActivityType.Steps, null, null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/activities", Walk(minutes: 0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/activities", Walk(Today.AddDays(5)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/activities", Steps(500_000))).StatusCode);
    }

    [Fact]
    public async Task Logging_steps_again_for_the_same_day_replaces_the_count()
    {
        var client = await factory.CreateAuthenticatedAsync("con_steps");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/activities", Steps(4000))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/activities", Steps(9000))).StatusCode);

        var entry = Assert.Single((await client.GetFromJsonAsync<List<ActivityDto>>("/api/activities"))!);
        Assert.Equal(9000, entry.Steps);
    }

    [Fact]
    public async Task Steps_ignore_session_fields_and_sessions_ignore_steps()
    {
        var client = await factory.CreateAuthenticatedAsync("con_fields");
        var steps = (await (await client.PostAsJsonAsync("/api/activities",
            new ActivityRequest(Today, ActivityType.Steps, 30, 4, 7000, null))).Content.ReadFromJsonAsync<ActivityDto>())!;
        var walk = (await (await client.PostAsJsonAsync("/api/activities",
            new ActivityRequest(Today, ActivityType.Walk, 30, 2, 9999, null))).Content.ReadFromJsonAsync<ActivityDto>())!;

        Assert.Null(steps.DurationMinutes);
        Assert.Null(steps.DistanceKm);
        Assert.Null(walk.Steps);
    }

    [Fact]
    public async Task Activities_can_be_edited_and_are_private()
    {
        var alice = await factory.CreateAuthenticatedAsync("con_alice");
        var bob = await factory.CreateAuthenticatedAsync("con_bob");
        var entry = (await (await alice.PostAsJsonAsync("/api/activities", Walk())).Content.ReadFromJsonAsync<ActivityDto>())!;

        var edited = await alice.PutAsJsonAsync($"/api/activities/{entry.Id}", Walk(minutes: 55));
        Assert.Equal(55, (await edited.Content.ReadFromJsonAsync<ActivityDto>())!.DurationMinutes);

        Assert.Empty((await bob.GetFromJsonAsync<List<ActivityDto>>("/api/activities"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/activities/{entry.Id}", Walk())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/activities/{entry.Id}")).StatusCode);
    }

    [Fact]
    public async Task Moving_a_step_count_onto_a_day_that_has_one_is_a_conflict()
    {
        var client = await factory.CreateAuthenticatedAsync("con_move");
        await client.PostAsJsonAsync("/api/activities", Steps(5000, Today));
        var other = (await (await client.PostAsJsonAsync("/api/activities", Steps(6000, Today.AddDays(-1)))).Content.ReadFromJsonAsync<ActivityDto>())!;

        var response = await client.PutAsJsonAsync($"/api/activities/{other.Id}", Steps(6000, Today));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // --- schedule ---

    [Fact]
    public async Task A_new_user_has_an_empty_schedule_and_the_default_step_goal()
    {
        var client = await factory.CreateAuthenticatedAsync("con_empty");
        var schedule = (await client.GetFromJsonAsync<ScheduleDto>("/api/schedule"))!;

        Assert.Empty(schedule.Days);
        Assert.Equal(8000, schedule.StepGoal);
        Assert.Null(schedule.Since);
    }

    [Fact]
    public async Task The_schedule_is_saved_in_monday_first_order_with_template_names()
    {
        var client = await factory.CreateAuthenticatedAsync("con_sched");
        var squat = (await client.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises"))!.First();
        var template = (await (await client.PostAsJsonAsync("/api/templates", new TemplateRequest("Leg day", null, [new(squat.Id, 3, 5)])))
            .Content.ReadFromJsonAsync<TemplateDto>())!;

        var response = await client.PutAsJsonAsync("/api/schedule", new ScheduleRequest(
            [new(DayOfWeek.Sunday, null), new(DayOfWeek.Wednesday, template.Id), new(DayOfWeek.Monday, null)], 10_000));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var saved = (await response.Content.ReadFromJsonAsync<ScheduleDto>())!;
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Sunday], saved.Days.Select(d => d.DayOfWeek));
        Assert.Equal("Leg day", saved.Days[1].TemplateName);
        Assert.Equal(10_000, saved.StepGoal);
        Assert.Equal(Today, saved.Since);
    }

    [Fact]
    public async Task Saving_again_replaces_the_schedule()
    {
        var client = await factory.CreateAuthenticatedAsync("con_replace");
        await client.PutAsJsonAsync("/api/schedule", new ScheduleRequest([new(DayOfWeek.Monday, null), new(DayOfWeek.Friday, null)], 8000));
        var saved = (await (await client.PutAsJsonAsync("/api/schedule",
            new ScheduleRequest([new(DayOfWeek.Friday, null), new(DayOfWeek.Saturday, null)], 8000))).Content.ReadFromJsonAsync<ScheduleDto>())!;

        Assert.Equal([DayOfWeek.Friday, DayOfWeek.Saturday], saved.Days.Select(d => d.DayOfWeek));

        var cleared = (await (await client.PutAsJsonAsync("/api/schedule", new ScheduleRequest([], 8000))).Content.ReadFromJsonAsync<ScheduleDto>())!;
        Assert.Empty(cleared.Days);
    }

    [Fact]
    public async Task Changing_only_a_template_or_the_step_goal_keeps_the_measurement_start_date()
    {
        var client = await factory.CreateAuthenticatedAsync("con_since");
        await client.PutAsJsonAsync("/api/schedule", new ScheduleRequest([new(DayOfWeek.Monday, null)], 8000));
        var userId = (await client.GetFromJsonAsync<UserDto>("/api/auth/me"))!.Id;
        var earlier = Today.AddDays(-20);
        await factory.WithDbAsync(async db =>
        {
            (await db.ConsistencySettings.SingleAsync(s => s.UserId == userId)).ScheduleSince = earlier;
            await db.SaveChangesAsync();
        });

        var sameDays = (await (await client.PutAsJsonAsync("/api/schedule", new ScheduleRequest([new(DayOfWeek.Monday, null)], 12_000))).Content.ReadFromJsonAsync<ScheduleDto>())!;
        Assert.Equal(earlier, sameDays.Since);

        var moreDays = (await (await client.PutAsJsonAsync("/api/schedule", new ScheduleRequest([new(DayOfWeek.Monday, null), new(DayOfWeek.Friday, null)], 12_000))).Content.ReadFromJsonAsync<ScheduleDto>())!;
        Assert.Equal(Today, moreDays.Since);
    }

    [Fact]
    public async Task Invalid_schedules_are_rejected()
    {
        var alice = await factory.CreateAuthenticatedAsync("con_inv_a");
        var bob = await factory.CreateAuthenticatedAsync("con_inv_b");
        var squat = (await bob.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises"))!.First();
        var bobsTemplate = (await (await bob.PostAsJsonAsync("/api/templates", new TemplateRequest("Mine", null, [new(squat.Id, 3, 5)])))
            .Content.ReadFromJsonAsync<TemplateDto>())!;

        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PutAsJsonAsync("/api/schedule",
            new ScheduleRequest([new(DayOfWeek.Monday, null), new(DayOfWeek.Monday, null)], 8000))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PutAsJsonAsync("/api/schedule",
            new ScheduleRequest([new(DayOfWeek.Monday, bobsTemplate.Id)], 8000))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PutAsJsonAsync("/api/schedule",
            new ScheduleRequest([new(DayOfWeek.Monday, null)], 10))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PutAsJsonAsync("/api/schedule?tz=5000",
            new ScheduleRequest([new(DayOfWeek.Monday, null)], 8000))).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_template_keeps_the_scheduled_day()
    {
        var client = await factory.CreateAuthenticatedAsync("con_tdel");
        var squat = (await client.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises"))!.First();
        var template = (await (await client.PostAsJsonAsync("/api/templates", new TemplateRequest("Temp", null, [new(squat.Id, 3, 5)])))
            .Content.ReadFromJsonAsync<TemplateDto>())!;
        await client.PutAsJsonAsync("/api/schedule", new ScheduleRequest([new(DayOfWeek.Tuesday, template.Id)], 8000));

        await client.DeleteAsync($"/api/templates/{template.Id}");

        var day = Assert.Single((await client.GetFromJsonAsync<ScheduleDto>("/api/schedule"))!.Days);
        Assert.Equal(DayOfWeek.Tuesday, day.DayOfWeek);
        Assert.Null(day.TemplateId);
        Assert.Null(day.TemplateName);
    }

    // --- the consistency view ---

    [Fact]
    public async Task Workouts_activities_and_steps_all_make_a_day_active()
    {
        var client = await factory.CreateAuthenticatedAsync("con_active");
        await LogWorkout(client, DateTime.UtcNow);
        await client.PostAsJsonAsync("/api/activities", Walk(Today.AddDays(-1)));
        await client.PostAsJsonAsync("/api/activities", Steps(9000, Today.AddDays(-2)));
        await client.PostAsJsonAsync("/api/activities", Steps(3000, Today.AddDays(-3)));

        var result = await Consistency(client);

        Assert.Equal(3, result.Summary.ActiveDays30);
        var byDate = result.Heatmap.ToDictionary(c => c.Date);
        Assert.Equal(1, byDate[Today].Workouts);
        Assert.Equal(1, byDate[Today.AddDays(-1)].Activities);
        Assert.Equal(30, byDate[Today.AddDays(-1)].Minutes);
        Assert.Equal(1, byDate[Today.AddDays(-2)].Level);
        Assert.Equal(0, byDate[Today.AddDays(-3)].Level); // under the 8,000 goal
    }

    [Fact]
    public async Task The_step_goal_comes_from_the_schedule_settings()
    {
        var client = await factory.CreateAuthenticatedAsync("con_goal");
        await client.PostAsJsonAsync("/api/activities", Steps(5000, Today.AddDays(-1)));
        Assert.Equal(0, (await Consistency(client)).Summary.ActiveDays30);

        await client.PutAsJsonAsync("/api/schedule", new ScheduleRequest([new(DayOfWeek.Monday, null)], 4000));

        var result = await Consistency(client);
        Assert.Equal(4000, result.StepGoal);
        Assert.Equal(1, result.Summary.ActiveDays30);
    }

    [Fact]
    public async Task Todays_scheduled_day_is_done_once_something_is_logged()
    {
        var client = await factory.CreateAuthenticatedAsync("con_today");
        await client.PutAsJsonAsync("/api/schedule", new ScheduleRequest([new(DateTime.UtcNow.DayOfWeek, null)], 8000));

        var before = (await Consistency(client)).Week.Single(d => d.IsToday);
        Assert.Equal(DayState.Pending, before.State);

        await client.PostAsJsonAsync("/api/activities", Walk());

        var after = await Consistency(client);
        Assert.Equal(DayState.Done, after.Week.Single(d => d.IsToday).State);
        Assert.Equal(1, after.Summary.Streak.Current);
        Assert.True(after.HasSchedule);
    }

    [Fact]
    public async Task Days_follow_the_users_own_clock_not_utc()
    {
        var client = await factory.CreateAuthenticatedAsync("con_tz");
        var lateYesterdayUtc = DateTime.SpecifyKind(Today.AddDays(-1).ToDateTime(new TimeOnly(23, 30)), DateTimeKind.Utc);
        await LogWorkout(client, lateYesterdayUtc);

        // In UTC it's yesterday evening. For someone two hours ahead (UTC+2) it is already today.
        Assert.Contains((await Consistency(client, tz: 0)).Heatmap, c => c.Date == Today.AddDays(-1));
        var ahead = await Consistency(client, tz: -120);
        Assert.Contains(ahead.Heatmap, c => c.Date == Today);
        Assert.DoesNotContain(ahead.Heatmap, c => c.Date == Today.AddDays(-1));

        // And five hours behind (UTC-5) it's still the evening of yesterday.
        Assert.Contains((await Consistency(client, tz: 300)).Heatmap, c => c.Date == Today.AddDays(-1));
    }

    [Fact]
    public async Task An_invalid_time_zone_offset_is_rejected()
    {
        var client = await factory.CreateAuthenticatedAsync("con_badtz");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/consistency?tz=5000")).StatusCode);
    }

    [Fact]
    public async Task A_new_user_has_a_quiet_but_well_formed_view()
    {
        var client = await factory.CreateAuthenticatedAsync("con_new");
        var result = await Consistency(client);

        Assert.False(result.HasSchedule);
        Assert.Equal(7, result.Week.Count);
        Assert.Equal(12, result.Weeks.Count);
        Assert.Empty(result.Heatmap);
        Assert.Equal(0, result.Summary.ActiveDays365);
    }

    [Fact]
    public async Task Consistency_is_private_and_removed_with_the_account()
    {
        var alice = await factory.CreateAuthenticatedAsync("con_priv_a");
        var bob = await factory.CreateAuthenticatedAsync("con_priv_b");
        await alice.PostAsJsonAsync("/api/activities", Walk());
        await alice.PutAsJsonAsync("/api/schedule", new ScheduleRequest([new(DayOfWeek.Monday, null)], 8000));

        Assert.Empty((await Consistency(bob)).Heatmap);
        Assert.Empty((await bob.GetFromJsonAsync<ScheduleDto>("/api/schedule"))!.Days);

        var aliceId = (await alice.GetFromJsonAsync<UserDto>("/api/auth/me"))!.Id;
        await alice.PostAsJsonAsync("/api/account/delete", new DeleteAccountRequest("correct horse battery"));
        await factory.WithDbAsync(async db =>
        {
            Assert.False(await db.Activities.AnyAsync(a => a.UserId == aliceId));
            Assert.False(await db.ScheduledDays.AnyAsync(d => d.UserId == aliceId));
            Assert.False(await db.ConsistencySettings.AnyAsync(s => s.UserId == aliceId));
        });
    }

    [Fact]
    public async Task Everything_here_needs_a_login()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/activities")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/activities", Walk())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/schedule")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/consistency")).StatusCode);
    }
}
