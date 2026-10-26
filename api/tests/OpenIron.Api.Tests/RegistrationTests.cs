using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Contracts;

namespace OpenIron.Api.Tests;

// Closing registration is instance-wide, so these get a database of their own.
public class RegistrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Registration_can_be_closed_without_affecting_existing_users()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("sess_before", "supersecret1"));
        Assert.True((await client.GetFromJsonAsync<AuthConfigDto>("/api/auth/config"))!.RegistrationOpen);

        await factory.WithDbAsync(async db =>
        {
            (await db.ServerSettings.FirstAsync()).RegistrationOpen = false;
            await db.SaveChangesAsync();
        });

        Assert.False((await client.GetFromJsonAsync<AuthConfigDto>("/api/auth/config"))!.RegistrationOpen);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("sess_after", "supersecret1"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("sess_before", "supersecret1"))).StatusCode);
    }
}
