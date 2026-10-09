using Moongate.Admin.Api.Data.Accounts;

namespace Moongate.Admin.Api.Data.Internal.Sessions;

public sealed class UpstreamLoginResult
{
    public AccountSummaryResponse Account { get; init; } = new();
    public string AccessToken { get; init; } = "";
    public DateTimeOffset ExpiresAt { get; init; }
}
