namespace OpenIron.Api.Data.Entities;

public enum InviteStatus
{
    Active,
    Used,
    Revoked,
    Expired,
}

/// <summary>A single-use code that lets one person create an account while sign-up is by invitation only.</summary>
public class Invite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>SHA-256 of the normalised code. The code itself is shown once when it is created and never stored.</summary>
    public required string CodeHash { get; set; }

    /// <summary>A reminder for the admin of who the code is for.</summary>
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public Guid? UsedByUserId { get; set; }
    public DateTime? RevokedAt { get; set; }

    public InviteStatus StatusAt(DateTime now) =>
        UsedAt is not null ? InviteStatus.Used
        : RevokedAt is not null ? InviteStatus.Revoked
        : ExpiresAt is not null && ExpiresAt <= now ? InviteStatus.Expired
        : InviteStatus.Active;
}
