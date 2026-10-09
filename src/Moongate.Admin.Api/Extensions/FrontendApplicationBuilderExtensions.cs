using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Moongate.Admin.Api.Extensions;

public static class FrontendApplicationBuilderExtensions
{
    private const string DefaultPath = "../../frontend/dist";
    private static readonly string[] ReservedPrefixes = ["/api", "/health", "/swagger", "/openapi"];
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static string? ResolveFrontendRoot(this WebApplication app)
    {
        var configured = app.Configuration["Frontend:Path"];
        var path = Path.GetFullPath(
            string.IsNullOrWhiteSpace(configured) ? DefaultPath : configured,
            app.Environment.ContentRootPath
        );
        return File.Exists(Path.Combine(path, "index.html")) ? path : null;
    }

    public static WebApplication MapFrontendFallback(this WebApplication app)
    {
        var root = app.ResolveFrontendRoot();
        var files = root is null ? null : new PhysicalFileProvider(root);
        app.MapFallback(
                "/{**path}",
                context =>
                {
                    var path = context.Request.Path;
                    if (files is null || !HttpMethods.IsGet(context.Request.Method) ||
                        ReservedPrefixes.Any(prefix => path.StartsWithSegments(prefix)))
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        return Task.CompletedTask;
                    }

                    if (Path.HasExtension(path.Value))
                    {
                        var file = files.GetFileInfo(path.Value!);
                        if (!file.Exists || file.PhysicalPath is null)
                        {
                            context.Response.StatusCode = StatusCodes.Status404NotFound;
                            return Task.CompletedTask;
                        }

                        context.Response.ContentType = ContentTypes.TryGetContentType(file.Name, out var type)
                            ? type
                            : "application/octet-stream";
                        context.Response.Headers.CacheControl = path.StartsWithSegments("/assets")
                            ? "public, max-age=31536000, immutable"
                            : "no-cache";
                        return context.Response.SendFileAsync(file.PhysicalPath);
                    }

                    context.Response.Headers.CacheControl = "no-store";
                    context.Response.ContentType = "text/html; charset=utf-8";
                    return context.Response.SendFileAsync(files.GetFileInfo("index.html").PhysicalPath!);
                }
            )
            .AllowAnonymous()
            .ExcludeFromDescription();
        return app;
    }
}
