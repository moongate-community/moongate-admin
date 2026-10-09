using Microsoft.Extensions.Options;
using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Data.Sessions;
using Moongate.Admin.Api.Interfaces.Upstream;

namespace Moongate.Admin.Api.Extensions;

public static class ServerEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapAdminServers(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth/session", SessionAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> SessionAsync(
        HttpContext context, IMoongateAdminClient client, IOptions<MoongateOptions> options, CancellationToken cancellationToken
    )
    {
        var session = context.GetAdminSession();
        await client.GetServerInfoAsync(options.Value.AuthenticationEndpointId, session.AccessToken, cancellationToken);
        return TypedResults.Ok(new SessionResponse { Account = session.Account, ExpiresAt = session.ExpiresAt });
    }
}
