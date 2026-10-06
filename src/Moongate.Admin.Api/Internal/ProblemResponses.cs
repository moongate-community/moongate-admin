using Microsoft.AspNetCore.WebUtilities;

namespace Moongate.Admin.Api.Internal;

public static class ProblemResponses
{
    public static async Task WriteAsync(HttpContext context, int status, string code, IDictionary<string, object?>? extra = null)
    {
        var extensions = extra ?? new Dictionary<string, object?>();
        extensions["code"] = code;
        extensions["correlationId"] = context.TraceIdentifier;
        context.Response.Headers.CacheControl = "no-store";
        await Results.Problem(statusCode: status, title: ReasonPhrases.GetReasonPhrase(status), extensions: extensions).ExecuteAsync(context);
    }
}
