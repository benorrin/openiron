using System.ComponentModel.DataAnnotations;

namespace OpenIron.Api.Contracts;

public record RegisterRequest(
    [Required, RegularExpression(@"^[A-Za-z0-9_.-]{3,32}$", ErrorMessage = "Username must be 3-32 characters: letters, numbers, '_', '.' or '-'.")]
    string Username,
    [Required, MinLength(8), MaxLength(128)]
    string Password);

public record LoginRequest([Required] string Username, [Required] string Password);

public record UserDto(Guid Id, string Username);

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);
