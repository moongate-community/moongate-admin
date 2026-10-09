using Moongate.Admin.Api.Internal;

namespace Moongate.Admin.Api.Services.Http;

public sealed class ApiTransportMiddleware
{
    public const string OperationItem = "MoongateAdmin.Operation";
    private readonly RequestDelegate _next;

    public ApiTransportMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if ((context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText is { } pattern)
        {
            context.Items[OperationItem] = context.Request.Method + " " + pattern;
        }

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!context.Request.IsHttps)
            {
                await ProblemResponses.WriteAsync(context, StatusCodes.Status400BadRequest, "https_required");
                return;
            }
        }

        await _next(context);
    }
}
