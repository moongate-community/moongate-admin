namespace Moongate.Admin.Api.Data.Sessions;

public sealed class SessionResponse
{
    public Accounts.AccountSummaryResponse Account { get; init; } = new();
    public DateTimeOffset ExpiresAt { get; init; }
}
