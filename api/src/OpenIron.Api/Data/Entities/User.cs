namespace OpenIron.Api.Data.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Username { get; set; }

    /// <summary>Lower-cased username, used for uniqueness and login lookups.</summary>
    public required string NormalizedUsername { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsAdmin { get; set; }

    /// <summary>Disabled users can't log in and any session they already have stops working.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// Embedded in login tokens and checked on every request. Changing it (on a password change or reset)
    /// signs the user out everywhere.
    /// </summary>
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    /// <summary>Whether other users on this server can see this user's training activity. Off until they opt in.</summary>
    public bool ShareActivity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
