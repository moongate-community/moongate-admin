namespace Moongate.Admin.Api.Types.Authentication;

public static class AdminAuthentication
{
    public const string Scheme = "Bearer";
    public const string AccountPolicy = "AdminAccounts";
    public const string Issuer = "moongate-admin";
    public const string Audience = "moongate-admin-api";
    public const string SessionItem = "MoongateAdmin.Session";
}
