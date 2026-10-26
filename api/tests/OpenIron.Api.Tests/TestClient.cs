using System.Net.Http.Headers;
using System.Net.Http.Json;
using OpenIron.Api.Contracts;

namespace OpenIron.Api.Tests;

public static class TestClient
{
    private static int _counter;

    /// <summary>Registers a fresh user and returns a client carrying their bearer token.</summary>
    public static async Task<HttpClient> CreateAuthenticatedAsync(this ApiFactory factory, string? username = null)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(username ?? $"user{Interlocked.Increment(ref _counter)}", "correct horse battery"));
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }
}
