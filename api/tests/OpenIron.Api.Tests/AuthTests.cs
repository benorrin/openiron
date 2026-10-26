using System.Net;
using System.Net.Http.Json;
using OpenIron.Api.Contracts;

namespace OpenIron.Api.Tests;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Register_then_login_returns_a_token_that_works_on_protected_routes()
    {
        var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("alice", "supersecret1"));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("ALICE", "supersecret1"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;

        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.Token);
        var me = await client.GetFromJsonAsync<UserDto>("/api/auth/me");
        Assert.Equal("alice", me!.Username);
    }

    [Fact]
    public async Task Register_rejects_duplicate_usernames_ignoring_case()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("bob", "supersecret1"));
        var again = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("BoB", "supersecret2"));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Theory]
    [InlineData("ab", "supersecret1")]
    [InlineData("has space", "supersecret1")]
    [InlineData("carol", "short")]
    public async Task Register_validates_input(string username, string password)
    {
        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/auth/register", new RegisterRequest(username, password));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_fails_for_wrong_password_and_unknown_user()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("dave", "supersecret1"));

        var wrong = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("dave", "nope-nope"));
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody", "supersecret1"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    }

    [Fact]
    public async Task Protected_routes_require_a_token()
    {
        var response = await factory.CreateClient().GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
