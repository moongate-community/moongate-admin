namespace Moongate.Admin.Api.Types.Authentication;

public static class AdminAuthentication
{
    public const string Scheme = "MoongateAdmin";
    public const string Cookie = "__Host-MoongateAdmin";
    public const string CsrfCookie = "__Host-MoongateAdmin.Csrf";
    public const string CsrfHeader = "X-CSRF-TOKEN";
    public const string AccountPolicy = "AdminAccounts";
    public const string TokenName = "moongate_access_token";
    public const string AccountProperty = "moongate_account";
}
