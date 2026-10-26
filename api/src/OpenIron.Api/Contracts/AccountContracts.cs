using System.ComponentModel.DataAnnotations;

namespace OpenIron.Api.Contracts;

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8), MaxLength(128)] string NewPassword);

public record DeleteAccountRequest([Required] string Password);
