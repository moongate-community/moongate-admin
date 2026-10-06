namespace Moongate.Admin.Api.Data.Accounts;

public sealed class AccountPageResponse
{
    public IReadOnlyList<AccountSummaryResponse> Accounts { get; init; } = [];
    public uint NextAfterAccountId { get; init; }
}
