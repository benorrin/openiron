using System.ComponentModel.DataAnnotations;

namespace OpenIron.Api.Auth;

public class JwtOptions
{
    public const string Section = "Jwt";

    [Required, MinLength(32)]
    public string Secret { get; set; } = "";
    public string Issuer { get; set; } = "openiron";
    public string Audience { get; set; } = "openiron";
    public int ExpiryMinutes { get; set; } = 60 * 24 * 7;
}
