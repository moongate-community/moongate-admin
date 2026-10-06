using Moongate.Admin.Api.Data.Accounts;
using Moongate.Admin.Api.Data.Sessions;
using Moongate.Admin.Api.Interfaces.Upstream;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Authentication;
using Serilog;
using Moongate.Admin.Api.Data.Internal.Configuration;

namespace Moongate.Admin.Api.Extensions;

public static class AuthEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapAdminAuth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/login", LoginAsync).Produces<JwtLoginResponse>().AllowAnonymous();
        endpoints.MapPost("/api/auth/logout", LogoutAsync).Produces(StatusCodes.Status204NoContent).AllowAnonymous();
        return endpoints;
    }
    private static async Task<IResult> LoginAsync(LoginRequest request, HttpContext context, IMoongateAdminClient client,
        AdminSessionAccessor accessor, JwtSessionService sessions, ConnectionCatalogSnapshot snapshot, CancellationToken cancellationToken)
    {
        var old = await accessor.GetAsync(context, cancellationToken);
        var login = await client.LoginAsync(request, cancellationToken);
        var response = sessions.Create(login, snapshot.Revision);
        if (old is not null)
        {
            sessions.Remove(old.SessionId);
            try
            {
                await client.LogoutAsync(old.AccessToken, cancellationToken);
            }
            catch (UpstreamCallException)
            {
                Log.ForContext<JwtSessionService>().Warning("Previous upstream session revocation was not confirmed for account {AccountId}", old.Account.AccountId);
            }
        }
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(response);
    }
    private static async Task<IResult> LogoutAsync(HttpContext context, IMoongateAdminClient client,
        AdminSessionAccessor accessor, JwtSessionService sessions, CancellationToken cancellationToken)
    {
        var session = await accessor.GetAsync(context, cancellationToken);
        try
        {
            if (session is not null)
            {
                await client.LogoutAsync(session.AccessToken, cancellationToken);
            }
        }
        finally
        {
            if (session is not null)
            {
                sessions.Remove(session.SessionId);
            }
            context.Items["LocalSessionCleared"] = true;
            context.Response.Headers.CacheControl = "no-store";
        }
        return TypedResults.NoContent();
    }
}
