namespace Moongate.Admin.Api.Data.Accounts;

public sealed class LoginRequest
{
    public string? Username { get; init; }
    public string? Password { get; init; }
}
