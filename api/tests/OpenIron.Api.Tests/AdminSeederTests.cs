using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIron.Api.Auth;
using OpenIron.Api.Data;
using OpenIron.Api.Data.Entities;

namespace OpenIron.Api.Tests;

public class AdminSeederTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly AppDbContext _db;
    private readonly PasswordHasher<User> _hasher = new();

    public AdminSeederTests()
    {
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Task Seed(string? username, string? password) =>
        AdminSeeder.EnsureAsync(_db, _hasher, new AdminOptions { Username = username, Password = password }, NullLogger.Instance);

    [Fact]
    public async Task Creates_the_configured_admin_with_the_configured_password()
    {
        await Seed("Boss", "a-long-password");

        var user = await _db.Users.SingleAsync();
        Assert.True(user.IsAdmin);
        Assert.Equal("Boss", user.Username);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(user, user.PasswordHash, "a-long-password"));
    }

    [Fact]
    public async Task Does_nothing_when_no_admin_is_configured()
    {
        await Seed(null, null);
        await Seed("  ", "whatever-password");
        Assert.Empty(await _db.Users.ToListAsync());
    }

    [Fact]
    public async Task Promotes_an_existing_account_without_touching_its_password()
    {
        var existing = new User { Username = "Boss", NormalizedUsername = "boss", PasswordHash = "" };
        existing.PasswordHash = _hasher.HashPassword(existing, "original-password");
        existing.IsDisabled = true;
        var stampBefore = existing.SecurityStamp;
        _db.Users.Add(existing);
        await _db.SaveChangesAsync();

        await Seed("boss", "a-different-password");

        var user = await _db.Users.SingleAsync();
        Assert.NotEqual(stampBefore, user.SecurityStamp);
        Assert.True(user.IsAdmin);
        Assert.False(user.IsDisabled);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(user, user.PasswordHash, "original-password"));
    }

    [Fact]
    public async Task Refuses_to_create_an_admin_with_a_weak_password()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Seed("boss", "short"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Seed("boss", null));
    }

    [Fact]
    public async Task Is_idempotent()
    {
        await Seed("boss", "a-long-password");
        await Seed("boss", "a-long-password");
        Assert.Single(await _db.Users.ToListAsync());
    }
}
