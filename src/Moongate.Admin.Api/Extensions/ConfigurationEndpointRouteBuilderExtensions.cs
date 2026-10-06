using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Data.Configuration;
using Moongate.Admin.Api.Data.Internal.Configuration;
using Moongate.Admin.Api.Interfaces.Configuration;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Config;
using Moongate.Admin.Api.Types.Authentication;

namespace Moongate.Admin.Api.Extensions;

public static class ConfigurationEndpointRouteBuilderExtensions
{
    private const int QuotedRevisionLength = 34;

    public static IEndpointRouteBuilder MapAdminConfiguration(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/configuration/status",
                (IConnectionCatalogStore store, SetupTokenService tokens) =>
                    TypedResults.Ok(
                        new ConfigurationStatusResponse
                        {
                            Configured = store.Current.Configured,
                            SetupAvailable = !store.Current.Configured && tokens.Available
                        }
                    )
            )
            .AllowAnonymous()
            .WithTags("Configuration")
            .WithSummary("Read setup status");
        endpoints.MapPost("/api/configuration/setup", SetupAsync)
            .AllowAnonymous()
            .Produces<ConnectionConfigurationResponse>(StatusCodes.Status201Created)
            .WithTags("Configuration")
            .WithSummary("Initialize Moongate connections");
        endpoints.MapGet("/api/configuration", ReadAsync)
            .RequireAuthorization(AdminAuthentication.AccountPolicy)
            .Produces<ConnectionConfigurationResponse>()
            .WithTags("Configuration")
            .WithSummary("Read Moongate connections");
        endpoints.MapPut("/api/configuration", ReplaceAsync)
            .RequireAuthorization(AdminAuthentication.AccountPolicy)
            .Produces<ConnectionConfigurationResponse>()
            .WithTags("Configuration")
            .WithSummary("Replace Moongate connections");
        endpoints.MapPost("/api/configuration/test-connection", ProbeAsync)
            .AllowAnonymous()
            .Produces<ConnectionProbeResponse>()
            .WithTags("Configuration")
            .WithSummary("Test a candidate Moongate connection");
        return endpoints;
    }

    private static async Task<IResult> SetupAsync(
        MoongateOptions configuration, HttpContext context,
        ConfigurationAccessService access, IConnectionCatalogStore store, CancellationToken cancellationToken
    )
    {
        access.RequireSetupToken(context);
        var snapshot = await store.SetupAsync(configuration, cancellationToken);
        context.Response.Headers.ETag = QuoteRevision(snapshot.Revision);
        return TypedResults.Json(
            ConnectionConfigurationResponse.FromSnapshot(snapshot),
            statusCode: StatusCodes.Status201Created
        );
    }

    private static async Task<IResult> ReadAsync(
        HttpContext context, ConfigurationAccessService access,
        ConnectionCatalogSnapshot snapshot, CancellationToken cancellationToken
    )
    {
        await access.RequireAdministratorAsync(context, cancellationToken);
        context.Response.Headers.ETag = QuoteRevision(snapshot.Revision);
        return TypedResults.Ok(ConnectionConfigurationResponse.FromSnapshot(snapshot));
    }

    private static async Task<IResult> ReplaceAsync(
        MoongateOptions configuration, HttpContext context,
        ConfigurationAccessService access, IConnectionCatalogStore store, CancellationToken cancellationToken
    )
    {
        await access.RequireAdministratorAsync(context, cancellationToken);
        var tags = context.Request.Headers.IfMatch;
        if (tags.Count == 0)
        {
            throw new ConfigurationException(
                StatusCodes.Status428PreconditionRequired,
                "configuration_precondition_required"
            );
        }

        var tag = tags.Count == 1 ? tags[0] : null;
        if (tag is null || tag.Length != QuotedRevisionLength || tag[0] != '"' || tag[^1] != '"' ||
            !Guid.TryParseExact(tag[1..^1], "N", out _))
        {
            throw new ConfigurationException(StatusCodes.Status412PreconditionFailed, "configuration_changed");
        }

        var snapshot = await store.ReplaceAsync(configuration, tag[1..^1], cancellationToken);
        context.Response.Headers.ETag = QuoteRevision(snapshot.Revision);
        return TypedResults.Ok(ConnectionConfigurationResponse.FromSnapshot(snapshot, reauthenticationRequired: true));
    }

    private static async Task<IResult> ProbeAsync(
        ConnectionProbeRequest request, HttpContext context,
        ConfigurationAccessService access, ConnectionProbeService probe, CancellationToken cancellationToken
    )
    {
        await access.RequireProbeAccessAsync(context, cancellationToken);
        return TypedResults.Ok(await probe.TestAsync(request, cancellationToken));
    }

    private static string QuoteRevision(string revision)
    {
        return "\"" + revision + "\"";
    }
}
