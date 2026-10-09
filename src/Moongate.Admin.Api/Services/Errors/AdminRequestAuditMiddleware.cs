using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Moongate.Admin.Api.Services.Http;

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
                var operation = context.Items[ApiTransportMiddleware.OperationItem] as string ?? "unmapped";
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
