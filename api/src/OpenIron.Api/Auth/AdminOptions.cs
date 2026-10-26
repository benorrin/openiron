namespace OpenIron.Api.Auth;

/// <summary>Optionally names an account that is created (or promoted) as an admin on startup.</summary>
public class AdminOptions
{
    public const string Section = "Admin";

    public string? Username { get; set; }
    public string? Password { get; set; }
}
