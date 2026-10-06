using System.ComponentModel.DataAnnotations;

namespace Moongate.Admin.Api.Data.Accounts;

public sealed class LoginRequest
{
    [Required][MaxLength(255)] public string? Username { get; init; }
    [Required] public string? Password { get; init; }
}
