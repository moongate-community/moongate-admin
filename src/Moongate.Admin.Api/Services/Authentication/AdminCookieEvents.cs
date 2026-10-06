using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Moongate.Admin.Api.Internal;

namespace Moongate.Admin.Api.Services.Authentication;

public sealed class AdminCookieEvents : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        return ProblemResponses.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "authentication_required");
    }
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        return ProblemResponses.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, "permission_denied");
    }
}
