using System.Security.Claims;
using Serilog;

namespace Moongate.Admin.Api.Services.Errors;

public sealed class AdminRequestAuditMiddleware
{
    private readonly Serilog.ILogger _logger = Log.ForContext<AdminRequestAuditMiddleware>();
    private readonly RequestDelegate _next;

    public AdminRequestAuditMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var operation = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmapped";
        try
        {
            await _next(context);
        }
        finally
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                _logger.Information(
                    "Administration operation {Operation} account {AccountId} status {Status} correlation {CorrelationId}",
                    operation,
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                    context.Response.StatusCode,
                    context.TraceIdentifier
                );
            }
        }
    }
}
