using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Moongate.Admin.Api.Data.Accounts;
using Moongate.Admin.Api.Data.Sessions;
using Moongate.Admin.Api.Interfaces.Upstream;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Authentication;
using Moongate.Admin.Api.Types.Authentication;
using Serilog;

namespace Moongate.Admin.Api.Extensions;

public static class AuthEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapAdminAuth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var tokens = antiforgery.GetAndStoreTokens(context);
            return TypedResults.Ok(new CsrfResponse { RequestToken = tokens.RequestToken ?? throw new InvalidOperationException("Missing antiforgery token.") });
        }).AllowAnonymous();
        endpoints.MapPost("/api/auth/login", LoginAsync).AllowAnonymous().AddEndpointFilter<CsrfValidationFilter>();
        endpoints.MapPost("/api/auth/logout", LogoutAsync).AllowAnonymous().AddEndpointFilter<CsrfValidationFilter>();
        return endpoints;
    }
    private static async Task<IResult> LoginAsync(LoginRequest request, HttpContext context, IMoongateAdminClient client, AdminSessionAccessor sessions, CancellationToken cancellationToken)
    {
        var oldSession = await sessions.GetAsync(context, cancellationToken);
        var login = await client.LoginAsync(request, cancellationToken);
        if (oldSession is not null)
        {
            await context.SignOutAsync();
            try
            {
                await client.LogoutAsync(oldSession.AccessToken, cancellationToken);
            }
            catch (UpstreamCallException)
            {
                Log.ForContext<AdminSessionAccessor>().Warning("Previous upstream session revocation was not confirmed for account {AccountId}", oldSession.Account.AccountId);
            }
        }
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, login.Account.AccountId.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, login.Account.Username),
            new Claim(ClaimTypes.Role, login.Account.AccountType.ToString().ToLowerInvariant())
        ], AdminAuthentication.Scheme);
        var properties = new AuthenticationProperties
        {
            ExpiresUtc = login.ExpiresAt, IsPersistent = true, AllowRefresh = false
        };
        properties.StoreTokens([new AuthenticationToken { Name = AdminAuthentication.TokenName, Value = login.AccessToken }]);
        properties.Items[AdminAuthentication.AccountProperty] = JsonSerializer.Serialize(login.Account);
        await context.SignInAsync(AdminAuthentication.SignInScheme, new ClaimsPrincipal(identity), properties);
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new SessionResponse { Account = login.Account, ExpiresAt = login.ExpiresAt });
    }
    private static async Task<IResult> LogoutAsync(HttpContext context, IMoongateAdminClient client, AdminSessionAccessor sessions, CancellationToken cancellationToken)
    {
        var session = await sessions.GetAsync(context, cancellationToken);
        try
        {
            if (session is not null)
            {
                await client.LogoutAsync(session.AccessToken, cancellationToken);
            }
        }
        finally
        {
            await context.SignOutAsync();
            context.Items["LocalSessionCleared"] = true;
            context.Response.Headers.CacheControl = "no-store";
        }
        return TypedResults.NoContent();
    }
}
