using Moongate.Admin.Api.Data.Accounts;
using Moongate.Admin.Api.Data.Sessions;
using Moongate.Admin.Api.Interfaces.Upstream;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Authentication;
using Moongate.Admin.Api.Types.Authentication;

namespace Moongate.Admin.Api.Extensions;

public static class AuthEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapAdminAuth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/login", LoginAsync).Produces<JwtLoginResponse>().AllowAnonymous();
        endpoints.MapPost("/api/auth/logout", LogoutAsync).Produces(StatusCodes.Status204NoContent).AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request, HttpContext context, IMoongateAdminClient client, JwtSessionService sessions,
        ILoggerFactory loggers, CancellationToken cancellationToken
    )
    {
        var previous = context.TryGetAdminSession();
        var login = await client.LoginAsync(request, cancellationToken);
        var response = sessions.Create(login);
        if (previous is not null)
        {
            sessions.Remove(previous.SessionId);
            try
            {
                await client.LogoutAsync(previous.AccessToken, cancellationToken);
            }
            catch (UpstreamCallException)
            {
                loggers.CreateLogger("Moongate.Admin.Api.Auth")
                    .LogWarning(
                        "Previous upstream session revocation was not confirmed for account {AccountId}",
                        previous.Account.AccountId
                    );
            }
        }

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context, IMoongateAdminClient client, JwtSessionService sessions, CancellationToken cancellationToken
    )
    {
        var session = context.TryGetAdminSession();
        if (session is null)
        {
            return TypedResults.NoContent();
        }

        try
        {
            await client.LogoutAsync(session.AccessToken, cancellationToken);
        }
        catch (UpstreamCallException)
        {
            context.Items[AdminAuthentication.LocalSessionClearedItem] = true;
            throw;
        }
        finally
        {
            sessions.Remove(session.SessionId);
        }

        return TypedResults.NoContent();
    }
}
