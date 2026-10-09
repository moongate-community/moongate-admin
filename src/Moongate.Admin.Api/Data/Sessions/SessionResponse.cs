using Moongate.Admin.Api.Data.Accounts;

namespace Moongate.Admin.Api.Data.Sessions;

public sealed class SessionResponse
{
    public AccountSummaryResponse Account { get; init; } = new();
    public DateTimeOffset ExpiresAt { get; init; }
}
