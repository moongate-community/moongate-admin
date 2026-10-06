namespace Moongate.Admin.Api.Data.Accounts;

public sealed class CreateAccountRequest
{
    public string? Username { get; init; }
    public string? Password { get; init; }
    public Types.Accounts.AdminAccountType? AccountType { get; init; }
    public bool CanAccessApi { get; init; }
}
