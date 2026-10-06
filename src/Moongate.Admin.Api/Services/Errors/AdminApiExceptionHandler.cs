using Grpc.Core;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Moongate.Admin.Api.Internal;
using Serilog;

namespace Moongate.Admin.Api.Services.Errors;

public sealed class AdminApiExceptionHandler : IExceptionHandler
{
    private readonly Serilog.ILogger _logger = Log.ForContext<AdminApiExceptionHandler>();
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (context.RequestAborted.IsCancellationRequested && exception is OperationCanceledException)
        {
            return false;
        }
        var status = exception switch
        {
            BadHttpRequestException bad => bad.StatusCode,
            UpstreamCallException call => call.StatusCode switch
            {
                StatusCode.InvalidArgument => 400, StatusCode.Unauthenticated => 401, StatusCode.PermissionDenied => 403,
                StatusCode.NotFound => 404, StatusCode.AlreadyExists => 409, StatusCode.ResourceExhausted => 429,
                StatusCode.Unavailable or StatusCode.Cancelled => 503, StatusCode.DeadlineExceeded => 504,
                StatusCode.Unimplemented => 501, _ => 502
            },
            _ => 500
        };
        if (status == 401 && context.User.Identity?.IsAuthenticated == true)
        {
            await context.SignOutAsync();
        }
        var code = exception is UpstreamCallException upstream ? "upstream_" + upstream.StatusCode.ToString().ToLowerInvariant() : "request_failed";
        var extra = new Dictionary<string, object?>();
        if (exception is UpstreamCallException { MutationOutcomeUnknown: true })
        {
            extra["mutationOutcomeUnknown"] = true;
        }
        if (context.Items.ContainsKey("LocalSessionCleared"))
        {
            extra["localSessionCleared"] = true;
        }
        _logger.Warning("Administration request failed with {Code} correlation {CorrelationId}", code, context.TraceIdentifier);
        await ProblemResponses.WriteAsync(context, status, code, extra);
        return true;
    }
}
