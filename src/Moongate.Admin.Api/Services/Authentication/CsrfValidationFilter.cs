using Microsoft.AspNetCore.Antiforgery;

namespace Moongate.Admin.Api.Services.Authentication;

public sealed class CsrfValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            await context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>()
                .ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            throw new BadHttpRequestException("Invalid antiforgery token.");
        }

        return await next(context);
    }
}
