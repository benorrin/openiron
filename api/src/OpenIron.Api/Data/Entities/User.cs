namespace OpenIron.Api.Data.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Username { get; set; }

    /// <summary>Lower-cased username, used for uniqueness and login lookups.</summary>
    public required string NormalizedUsername { get; set; }
    public required string PasswordHash { get; set; }
    /// <summary>Whether other users on this server can see this user's training activity. Off until they opt in.</summary>
    public bool ShareActivity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
