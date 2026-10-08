using Microsoft.Extensions.FileProviders;

namespace Moongate.Admin.Api.Extensions;

public static class FrontendApplicationBuilderExtensions
{
    public static void UseMoongateFrontend(this WebApplication app)
    {
        var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        var frontendRoot = Path.Combine(webRoot, "frontend");
        var indexPath = Path.Combine(frontendRoot, "index.html");
        if (Directory.Exists(frontendRoot))
        {
            var provider = new PhysicalFileProvider(frontendRoot);
            app.Lifetime.ApplicationStopped.Register(provider.Dispose);
            app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });
        }

        app.MapFallback(
                "/{**path}",
                async context =>
                {
                    var path = context.Request.Path;
                    var reserved = path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
                                   path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
                                   path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) ||
                                   path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase) ||
                                   path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase);
                    if ((!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)) ||
                        reserved || Path.HasExtension(path.Value) || !File.Exists(indexPath))
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        return;
                    }

                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.Headers.CacheControl = "no-store";
                    await context.Response.SendFileAsync(indexPath, context.RequestAborted);
                }
            )
            .AllowAnonymous()
            .ExcludeFromDescription();
    }
}
