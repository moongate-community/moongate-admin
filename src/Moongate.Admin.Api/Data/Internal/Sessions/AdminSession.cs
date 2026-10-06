namespace Moongate.Admin.Api.Data.Internal.Sessions;

public sealed class AdminSession
{
    public string SessionId { get; init; } = "";
    public string ConfigurationRevision { get; init; } = "";
    public Data.Accounts.AccountSummaryResponse Account { get; init; } = new();
    public string AccessToken { get; init; } = "";
    public DateTimeOffset ExpiresAt { get; init; }
}
