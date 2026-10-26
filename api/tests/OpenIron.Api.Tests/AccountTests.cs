using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Contracts;

namespace OpenIron.Api.Tests;

public class AccountTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct horse battery"; // what TestClient registers with

    [Fact]
    public async Task Changing_the_password_keeps_this_session_and_ends_others()
    {
        var phone = await factory.CreateAuthenticatedAsync("acct_pw");
        var laptop = factory.CreateClient();
        var login = await laptop.PostAsJsonAsync("/api/auth/login", new LoginRequest("acct_pw", Password));
        laptop.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<AuthResponse>())!.Token);

        var response = await phone.PutAsJsonAsync("/api/account/password", new ChangePasswordRequest(Password, "a brand new password"));
        response.EnsureSuccessStatusCode();
        var fresh = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;

        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.GetAsync("/api/auth/me")).StatusCode);

        phone.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fresh.Token);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/auth/me")).StatusCode);

        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest("acct_pw", Password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest("acct_pw", "a brand new password"))).StatusCode);
    }

    [Fact]
    public async Task A_wrong_current_password_is_a_400_so_the_app_does_not_sign_the_user_out()
    {
        var client = await factory.CreateAuthenticatedAsync("acct_wrong");

        var response = await client.PutAsJsonAsync("/api/account/password", new ChangePasswordRequest("not my password", "a brand new password"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task New_passwords_must_be_long_enough()
    {
        var client = await factory.CreateAuthenticatedAsync("acct_short");
        var response = await client.PutAsJsonAsync("/api/account/password", new ChangePasswordRequest(Password, "short"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_an_account_removes_everything_the_user_owns_and_nothing_else()
    {
        var leaver = await factory.CreateAuthenticatedAsync("acct_leaver");
        var stayer = await factory.CreateAuthenticatedAsync("acct_stayer");

        // The leaver owns a custom exercise that their own template and workout both reference.
        var custom = (await (await leaver.PostAsJsonAsync("/api/exercises", new ExerciseRequest("Odd lift", "Core")))
            .Content.ReadFromJsonAsync<ExerciseDto>())!;
        await leaver.PostAsJsonAsync("/api/templates", new TemplateRequest("T", null, [new(custom.Id, 3, 5)]));
        await leaver.PostAsJsonAsync("/api/workouts", new WorkoutRequest("W", DateTime.UtcNow, null, null, null,
            [new(custom.Id, [new SetRequest(5, 50)])]));
        await leaver.PostAsJsonAsync("/api/body-weight", new BodyWeightRequest(new DateOnly(2026, 1, 1), 80));

        var squat = (await stayer.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises"))!.First(e => e.Name == "Barbell Back Squat");
        await stayer.PostAsJsonAsync("/api/workouts", new WorkoutRequest("Mine", DateTime.UtcNow, null, null, null,
            [new(squat.Id, [new SetRequest(5, 100)])]));

        var response = await leaver.PostAsJsonAsync("/api/account/delete", new DeleteAccountRequest(Password));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await factory.WithDbAsync(async db =>
        {
            Assert.False(await db.Users.AnyAsync(u => u.Username == "acct_leaver"));
            Assert.False(await db.Exercises.AnyAsync(e => e.Name == "Odd lift"));
            Assert.False(await db.Workouts.AnyAsync(w => w.Name == "W"));
            Assert.False(await db.WorkoutTemplates.AnyAsync(t => t.Name == "T"));
            Assert.Equal(0, await db.BodyWeightEntries.CountAsync(e => e.Date == new DateOnly(2026, 1, 1) && e.WeightKg == 80));
            Assert.True(await db.Exercises.AnyAsync(e => e.UserId == null && e.Name == "Barbell Back Squat"));
            Assert.True(await db.Workouts.AnyAsync(w => w.Name == "Mine"));
        });
        Assert.Equal(HttpStatusCode.Unauthorized, (await leaver.GetAsync("/api/auth/me")).StatusCode);
        Assert.Single((await stayer.GetFromJsonAsync<List<WorkoutSummaryDto>>("/api/workouts"))!);
    }

    [Fact]
    public async Task Deleting_needs_the_right_password()
    {
        var client = await factory.CreateAuthenticatedAsync("acct_keep");

        var response = await client.PostAsJsonAsync("/api/account/delete", new DeleteAccountRequest("nope nope nope"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task The_only_admin_cannot_delete_their_own_account()
    {
        var client = await factory.CreateAuthenticatedAsync("acct_admin");
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.FirstAsync(u => u.Username == "acct_admin")).IsAdmin = true;
            await db.SaveChangesAsync();
        });

        var response = await client.PostAsJsonAsync("/api/account/delete", new DeleteAccountRequest(Password));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Account_endpoints_require_a_login()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PutAsJsonAsync("/api/account/password", new ChangePasswordRequest("a", "bbbbbbbbb"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PostAsJsonAsync("/api/account/delete", new DeleteAccountRequest("a"))).StatusCode);
    }
}
