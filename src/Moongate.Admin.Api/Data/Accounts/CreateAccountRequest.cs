using System.ComponentModel.DataAnnotations;

namespace Moongate.Admin.Api.Data.Accounts;

public sealed class CreateAccountRequest
{
    [Required][MaxLength(255)] public string? Username { get; init; }
    [Required] public string? Password { get; init; }
    public Types.Accounts.AdminAccountType? AccountType { get; init; }
    public bool CanAccessApi { get; init; }
}
