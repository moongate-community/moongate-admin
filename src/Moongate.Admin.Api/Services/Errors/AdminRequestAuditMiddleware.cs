using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Moongate.Admin.Api.Services.Errors;

public sealed class AdminRequestAuditMiddleware
{
    private readonly ILogger<AdminRequestAuditMiddleware> _logger;
    private readonly RequestDelegate _next;

    public AdminRequestAuditMiddleware(RequestDelegate next, ILogger<AdminRequestAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        finally
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                var operation = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmapped";
                _logger.LogInformation(
                    "Administration operation {Operation} account {AccountId} status {Status} correlation {CorrelationId}",
                    operation,
                    context.User.FindFirstValue(JwtRegisteredClaimNames.Sub),
                    context.Response.StatusCode,
                    context.TraceIdentifier
                );
            }
        }
    }
}
