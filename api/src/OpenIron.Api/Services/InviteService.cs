using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OpenIron.Api.Data;

namespace OpenIron.Api.Services;

public class InviteService(AppDbContext db)
{
    // 32 symbols with nothing easily misread (no 0/O or 1/I), so a code survives being read aloud or typed.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 12;

    /// <summary>A random code like <c>K7QF-2MXN-9WDA</c>: 60 bits from the system's secure random source.</summary>
    public static string NewCode()
    {
        var chars = new char[CodeLength + CodeLength / 4 - 1];
        var i = 0;
        for (var n = 0; n < CodeLength; n++)
        {
            if (n > 0 && n % 4 == 0) chars[i++] = '-';
            chars[i++] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(chars);
    }

    /// <summary>Case, spaces and hyphens don't matter when someone types a code in.</summary>
    public static string Normalize(string code) =>
        new(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code))));

    /// <summary>
    /// Uses up a code if it is valid, in one conditional update so two people can't both claim it. Call this
    /// inside the transaction that creates the account, so that if sign-up then fails the code isn't lost.
    /// </summary>
    public async Task<Guid?> TryClaimAsync(string? code, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var hash = Hash(code);
        var claimed = await db.Invites
            .Where(i => i.CodeHash == hash && i.UsedAt == null && i.RevokedAt == null && (i.ExpiresAt == null || i.ExpiresAt > now))
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.UsedAt, now));

        return claimed == 0 ? null : await db.Invites.Where(i => i.CodeHash == hash).Select(i => i.Id).SingleAsync();
    }

    public Task RecordUseAsync(Guid inviteId, Guid userId) =>
        db.Invites.Where(i => i.Id == inviteId).ExecuteUpdateAsync(s => s.SetProperty(i => i.UsedByUserId, userId));
}
