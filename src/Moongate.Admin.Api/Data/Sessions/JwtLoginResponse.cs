using Moongate.Admin.Api.Data.Accounts;

namespace Moongate.Admin.Api.Data.Sessions;

public sealed class JwtLoginResponse
{
    public string AccessToken { get; init; } = "";
    public string TokenType { get; init; } = "Bearer";
    public AccountSummaryResponse Account { get; init; } = new();
    public DateTimeOffset ExpiresAt { get; init; }
}
