namespace OpenIron.Api.Data.Entities;

/// <summary>Instance-wide settings an admin can change. There is exactly one row, with Id 1.</summary>
public class ServerSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>When false, new accounts can't be created through the registration endpoint.</summary>
    public bool RegistrationOpen { get; set; } = true;
}
