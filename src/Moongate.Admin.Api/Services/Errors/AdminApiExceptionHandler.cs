using Grpc.Core;
using Microsoft.AspNetCore.Diagnostics;
using Moongate.Admin.Api.Data.Internal.Sessions;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Authentication;
using Moongate.Admin.Api.Types.Authentication;

namespace Moongate.Admin.Api.Services.Errors;

public sealed class AdminApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<AdminApiExceptionHandler> _logger;

    public AdminApiExceptionHandler(ILogger<AdminApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (context.RequestAborted.IsCancellationRequested && exception is OperationCanceledException)
        {
            return false;
        }

        var status = exception switch
        {
            ConfigurationException configuration => configuration.StatusCode,
            BadHttpRequestException bad => bad.StatusCode,
            UpstreamCallException call => call.StatusCode switch
            {
                StatusCode.InvalidArgument => 400,
                StatusCode.Unauthenticated => 401,
                StatusCode.PermissionDenied => 403,
                StatusCode.NotFound => 404,
                StatusCode.AlreadyExists => 409,
                StatusCode.ResourceExhausted => 429,
                StatusCode.Unavailable or StatusCode.Cancelled => 503,
                StatusCode.DeadlineExceeded => 504,
                StatusCode.Unimplemented => 501,
                _ => 502
            },
            _ => 500
        };
        var clearsSession = exception is UpstreamCallException { StatusCode: StatusCode.Unauthenticated, InvalidatesLocalSession: true } &&
                            !context.Request.Path.StartsWithSegments("/api/auth/login");
        if ((clearsSession || context.Items.ContainsKey(AdminAuthentication.LocalSessionClearedItem)) &&
            context.Items[AdminAuthentication.SessionItem] is AdminSession session)
        {
            context.RequestServices.GetRequiredService<JwtSessionService>().Remove(session.SessionId);
        }

        var code = exception switch
        {
            ConfigurationException configuration => configuration.Code,
            UpstreamCallException upstream => "upstream_" + upstream.StatusCode.ToString().ToLowerInvariant(),
            BadHttpRequestException { StatusCode: StatusCodes.Status401Unauthorized } => "authentication_required",
            _ => status == 500 ? "request_failed" : "bad_request"
        };
        var extra = new Dictionary<string, object?>();
        if (status == StatusCodes.Status401Unauthorized)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        if (exception is UpstreamCallException { MutationOutcomeUnknown: true })
        {
            extra["mutationOutcomeUnknown"] = true;
        }

        if (context.Items.ContainsKey(AdminAuthentication.LocalSessionClearedItem))
        {
            extra["localSessionCleared"] = true;
        }

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unexpected administration failure, correlation {CorrelationId}", context.TraceIdentifier);
        }

        _logger.LogWarning("Administration request failed with {Code} correlation {CorrelationId}", code, context.TraceIdentifier);
        await ProblemResponses.WriteAsync(context, status, code, extra);
        return true;
    }
}
