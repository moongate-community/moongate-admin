namespace Moongate.Admin.Api.Extensions;

public static class AdminEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapMoongateAdmin(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
        endpoints.MapAdminAuth();
        endpoints.MapAdminServers();
        return endpoints;
    }
}
