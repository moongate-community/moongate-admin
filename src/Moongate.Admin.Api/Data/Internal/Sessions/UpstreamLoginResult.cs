namespace Moongate.Admin.Api.Data.Internal.Sessions;

public sealed class UpstreamLoginResult
{
    public Data.Accounts.AccountSummaryResponse Account { get; init; } = new();
    public string AccessToken { get; init; } = "";
    public DateTimeOffset ExpiresAt { get; init; }
}
