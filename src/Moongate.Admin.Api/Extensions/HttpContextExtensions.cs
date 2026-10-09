using Moongate.Admin.Api.Data.Internal.Sessions;
using Moongate.Admin.Api.Types.Authentication;

namespace Moongate.Admin.Api.Extensions;

public static class HttpContextExtensions
{
    public static AdminSession? TryGetAdminSession(this HttpContext context)
    {
        return context.Items[AdminAuthentication.SessionItem] as AdminSession;
    }

    public static AdminSession GetAdminSession(this HttpContext context)
    {
        return context.TryGetAdminSession()
               ?? throw new BadHttpRequestException("Authentication required.", StatusCodes.Status401Unauthorized);
    }
}
