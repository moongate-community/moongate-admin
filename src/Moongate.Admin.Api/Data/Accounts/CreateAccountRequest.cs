using System.ComponentModel.DataAnnotations;
using Moongate.Admin.Api.Types.Accounts;

namespace Moongate.Admin.Api.Data.Accounts;

public sealed class CreateAccountRequest
{
    [Required][MaxLength(255)] public string? Username { get; init; }
    [Required] public string? Password { get; init; }
    public AdminAccountType? AccountType { get; init; }
    public bool CanAccessApi { get; init; }
}
