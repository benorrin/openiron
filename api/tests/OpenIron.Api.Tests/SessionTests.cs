using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIron.Api.Contracts;

namespace OpenIron.Api.Tests;

public class SessionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private Task Update(string username, Action<Data.Entities.User> change) => factory.WithDbAsync(async db =>
    {
        var user = await db.Users.FirstAsync(u => u.Username == username);
        change(user);
        await db.SaveChangesAsync();
    });

    [Fact]
    public async Task Disabling_a_user_ends_their_existing_session_immediately()
    {
        var client = await factory.CreateAuthenticatedAsync("sess_disabled");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        await Update("sess_disabled", u => u.IsDisabled = true);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Disabled_users_cannot_log_in_but_a_wrong_password_reveals_nothing()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("sess_login", "supersecret1"));
        await Update("sess_login", u => u.IsDisabled = true);

        var right = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("sess_login", "supersecret1"));
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("sess_login", "not-the-password"));

        Assert.Equal(HttpStatusCode.Forbidden, right.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
    }

    [Fact]
    public async Task Changing_the_security_stamp_signs_the_user_out_everywhere()
    {
        var client = await factory.CreateAuthenticatedAsync("sess_stamp");
        await Update("sess_stamp", u => u.SecurityStamp = Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Tokens_without_a_stamp_are_rejected()
    {
        var client = await factory.CreateAuthenticatedAsync("sess_old");
        var id = (await client.GetFromJsonAsync<UserDto>("/api/auth/me"))!.Id;

        // Shaped like a token issued before stamps existed: correctly signed, but no stamp claim.
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-secret-test-secret-test-secret-123456"));
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "openiron", "openiron", [new Claim("sub", id.ToString())],
            expires: DateTime.UtcNow.AddHours(1), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Deleting_the_user_row_invalidates_their_token()
    {
        var client = await factory.CreateAuthenticatedAsync("sess_gone");
        await factory.WithDbAsync(async db =>
        {
            db.Users.Remove(await db.Users.FirstAsync(u => u.Username == "sess_gone"));
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Regular_users_are_not_admins()
    {
        var client = await factory.CreateAuthenticatedAsync("sess_plain");
        Assert.False((await client.GetFromJsonAsync<UserDto>("/api/auth/me"))!.IsAdmin);
    }
}
