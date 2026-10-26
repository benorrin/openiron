using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Contracts;

namespace OpenIron.Api.Tests;

public class AdminTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> CreateAdminAsync(string username)
    {
        var client = await factory.CreateAuthenticatedAsync(username);
        await SetAdmin(username, true);
        return client;
    }

    private Task SetAdmin(string username, bool value) => factory.WithDbAsync(async db =>
    {
        (await db.Users.FirstAsync(u => u.Username == username)).IsAdmin = value;
        await db.SaveChangesAsync();
    });

    private static async Task<Guid> IdOf(HttpClient client) => (await client.GetFromJsonAsync<UserDto>("/api/auth/me"))!.Id;

    [Fact]
    public async Task Admin_endpoints_are_forbidden_to_regular_users_and_need_a_login()
    {
        var user = await factory.CreateAuthenticatedAsync("adm_regular");
        var anon = factory.CreateClient();
        var id = await IdOf(user);

        foreach (var client in new[] { user, anon })
        {
            var expected = client == user ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized;
            Assert.Equal(expected, (await client.GetAsync("/api/admin/overview")).StatusCode);
            Assert.Equal(expected, (await client.GetAsync("/api/admin/users")).StatusCode);
            Assert.Equal(expected, (await client.GetAsync("/api/admin/settings")).StatusCode);
            Assert.Equal(expected, (await client.PutAsJsonAsync($"/api/admin/users/{id}", new AdminUserUpdate(true, null))).StatusCode);
            Assert.Equal(expected, (await client.DeleteAsync($"/api/admin/users/{id}")).StatusCode);
        }
    }

    [Fact]
    public async Task Users_list_shows_activity_counts_but_not_contents()
    {
        var admin = await CreateAdminAsync("adm_lister");
        var member = await factory.CreateAuthenticatedAsync("adm_member");
        var squat = (await member.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises"))!.First();
        await member.PostAsJsonAsync("/api/workouts", new WorkoutRequest("Secret session name", DateTime.UtcNow, null, "secret note", null,
            [new(squat.Id, [new SetRequest(5, 100)])]));
        await member.PostAsJsonAsync("/api/body-weight", new BodyWeightRequest(new DateOnly(2026, 1, 1), 91.234));

        var response = await admin.GetAsync("/api/admin/users");
        var json = await response.Content.ReadAsStringAsync();
        var users = (await response.Content.ReadFromJsonAsync<List<AdminUserDto>>())!;

        var row = users.Single(u => u.Username == "adm_member");
        Assert.Equal(1, row.WorkoutCount);
        Assert.NotNull(row.LastWorkoutAt);
        Assert.False(row.IsYou);
        Assert.True(users.Single(u => u.Username == "adm_lister").IsYou);
        Assert.DoesNotContain("Secret session name", json);
        Assert.DoesNotContain("secret note", json);
        Assert.DoesNotContain("91.234", json);
    }

    [Fact]
    public async Task Overview_counts_users_and_workouts()
    {
        var admin = await CreateAdminAsync("adm_overview");
        var overview = (await admin.GetFromJsonAsync<AdminOverviewDto>("/api/admin/overview"))!;

        Assert.True(overview.TotalUsers >= 1);
        Assert.True(overview.AdminCount >= 1);
        Assert.True(overview.RegistrationOpen);
    }

    [Fact]
    public async Task Granting_and_revoking_admin_takes_effect_on_the_next_request()
    {
        var admin = await CreateAdminAsync("adm_granter");
        var member = await factory.CreateAuthenticatedAsync("adm_grantee");
        var memberId = await IdOf(member);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/admin/users")).StatusCode);

        var granted = await admin.PutAsJsonAsync($"/api/admin/users/{memberId}", new AdminUserUpdate(true, null));
        Assert.True((await granted.Content.ReadFromJsonAsync<AdminUserDto>())!.IsAdmin);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/admin/users")).StatusCode);
        Assert.True((await member.GetFromJsonAsync<UserDto>("/api/auth/me"))!.IsAdmin);

        await admin.PutAsJsonAsync($"/api/admin/users/{memberId}", new AdminUserUpdate(false, null));
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Disabling_a_user_signs_them_out_and_enabling_lets_them_back_in()
    {
        var admin = await CreateAdminAsync("adm_disabler");
        var member = await factory.CreateAuthenticatedAsync("adm_disabled");
        var memberId = await IdOf(member);

        await admin.PutAsJsonAsync($"/api/admin/users/{memberId}", new AdminUserUpdate(null, true));
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/auth/me")).StatusCode);
        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("adm_disabled", "correct horse battery"));
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);

        await admin.PutAsJsonAsync($"/api/admin/users/{memberId}", new AdminUserUpdate(null, false));
        login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("adm_disabled", "correct horse battery"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Admins_cannot_act_on_their_own_account()
    {
        var admin = await CreateAdminAsync("adm_self");
        var id = await IdOf(admin);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/admin/users/{id}", new AdminUserUpdate(false, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/admin/users/{id}", new AdminUserUpdate(null, true))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PostAsJsonAsync($"/api/admin/users/{id}/reset-password", new AdminResetPasswordRequest("a new password"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/admin/users/{id}")).StatusCode);

        // And nothing changed: still an admin, still signed in.
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Resetting_a_password_replaces_it_and_ends_the_users_sessions()
    {
        var admin = await CreateAdminAsync("adm_resetter");
        var member = await factory.CreateAuthenticatedAsync("adm_forgot");
        var id = await IdOf(member);

        var reset = await admin.PostAsJsonAsync($"/api/admin/users/{id}/reset-password", new AdminResetPasswordRequest("temporary pass 1"));
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/auth/me")).StatusCode);
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest("adm_forgot", "correct horse battery"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest("adm_forgot", "temporary pass 1"))).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync($"/api/admin/users/{id}/reset-password", new AdminResetPasswordRequest("short"))).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_user_removes_them_and_their_data()
    {
        var admin = await CreateAdminAsync("adm_deleter");
        var member = await factory.CreateAuthenticatedAsync("adm_doomed");
        var id = await IdOf(member);
        var squat = (await member.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises"))!.First();
        await member.PostAsJsonAsync("/api/workouts", new WorkoutRequest("W", DateTime.UtcNow, null, null, null,
            [new(squat.Id, [new SetRequest(5, 100)])]));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/users/{id}")).StatusCode);

        Assert.DoesNotContain((await admin.GetFromJsonAsync<List<AdminUserDto>>("/api/admin/users"))!, u => u.Id == id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/auth/me")).StatusCode);
        await factory.WithDbAsync(async db => Assert.False(await db.Workouts.AnyAsync(w => w.UserId == id)));
    }

    [Fact]
    public async Task Unknown_users_and_empty_updates_are_rejected()
    {
        var admin = await CreateAdminAsync("adm_picky");
        var missing = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/admin/users/{missing}", new AdminUserUpdate(true, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/admin/users/{missing}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.PostAsJsonAsync($"/api/admin/users/{missing}/reset-password", new AdminResetPasswordRequest("a new password"))).StatusCode);

        var member = await factory.CreateAuthenticatedAsync("adm_untouched");
        var empty = await admin.PutAsJsonAsync($"/api/admin/users/{await IdOf(member)}", new AdminUserUpdate(null, null));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Admins_can_close_and_reopen_registration()
    {
        var admin = await CreateAdminAsync("adm_gatekeeper");
        var anon = factory.CreateClient();
        try
        {
            var closed = await admin.PutAsJsonAsync("/api/admin/settings", new AdminSettingsDto(false));
            Assert.False((await closed.Content.ReadFromJsonAsync<AdminSettingsDto>())!.RegistrationOpen);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await anon.PostAsJsonAsync("/api/auth/register", new RegisterRequest("adm_latecomer", "supersecret1"))).StatusCode);
            Assert.False((await admin.GetFromJsonAsync<AdminOverviewDto>("/api/admin/overview"))!.RegistrationOpen);
        }
        finally
        {
            await admin.PutAsJsonAsync("/api/admin/settings", new AdminSettingsDto(true));
        }

        Assert.Equal(HttpStatusCode.OK,
            (await anon.PostAsJsonAsync("/api/auth/register", new RegisterRequest("adm_latecomer", "supersecret1"))).StatusCode);
    }
}
