using Microsoft.Extensions.Options;
using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Data.Servers;
using Moongate.Admin.Api.Data.Sessions;
using Moongate.Admin.Api.Interfaces.Upstream;
using Moongate.Admin.Api.Services.Authentication;

namespace Moongate.Admin.Api.Extensions;

public static class ServerEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapAdminServers(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth/session", async (HttpContext context, AdminSessionAccessor sessions,
            IMoongateAdminClient client, IOptions<MoongateOptions> options, CancellationToken cancellationToken) =>
        {
            var session = await sessions.GetAsync(context, cancellationToken)
                ?? throw new BadHttpRequestException("Authentication required.", StatusCodes.Status401Unauthorized);
            await client.GetServerInfoAsync(options.Value.AuthenticationEndpointId, session.AccessToken, cancellationToken);
            return TypedResults.Ok(new SessionResponse { Account = session.Account, ExpiresAt = session.ExpiresAt });
        }).RequireAuthorization();
        endpoints.MapGet("/api/servers", async (HttpContext context, AdminSessionAccessor sessions,
            IMoongateAdminClient client, IOptions<MoongateOptions> options, CancellationToken cancellationToken) =>
        {
            var session = await sessions.GetAsync(context, cancellationToken)
                ?? throw new BadHttpRequestException("Authentication required.", StatusCodes.Status401Unauthorized);
            await client.GetServerInfoAsync(options.Value.AuthenticationEndpointId, session.AccessToken, cancellationToken);
            return TypedResults.Ok(options.Value.Endpoints.Select(endpoint => new ServerSummaryResponse
            {
                Id = endpoint.Id, Label = endpoint.Label
            }).ToArray());
        }).RequireAuthorization();
        endpoints.MapGet("/api/servers/{id}", async (string id, HttpContext context, AdminSessionAccessor sessions,
            IMoongateAdminClient client, CancellationToken cancellationToken) =>
        {
            var session = await sessions.GetAsync(context, cancellationToken)
                ?? throw new BadHttpRequestException("Authentication required.", StatusCodes.Status401Unauthorized);
            return TypedResults.Ok(await client.GetServerInfoAsync(id, session.AccessToken, cancellationToken));
        }).RequireAuthorization();
        return endpoints;
    }
}
