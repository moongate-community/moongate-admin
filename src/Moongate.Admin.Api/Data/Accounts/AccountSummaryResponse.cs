namespace Moongate.Admin.Api.Data.Accounts;

public sealed class AccountSummaryResponse
{
    public uint AccountId { get; init; }
    public string Username { get; init; } = "";
    public Types.Accounts.AdminAccountType AccountType { get; init; }
    public bool CanAccessApi { get; init; }
    public bool IsLocked { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
