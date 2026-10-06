using Moongate.Admin.Api.Extensions;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Errors;
using Serilog.Events;
using Serilog;

namespace Moongate.Admin.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSerilog((services, configuration) => configuration.MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .ReadFrom.Services(services)
            .WriteTo.Console()
        );
        builder.Services.AddMoongateAdmin(builder.Configuration, builder.Environment);
        var app = builder.Build();
        app.UseMiddleware<AdminRequestAuditMiddleware>();
        app.UseExceptionHandler();
        app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.Headers.CacheControl = "no-store";
                    if (!context.Request.IsHttps)
                    {
                        await ProblemResponses.WriteAsync(context, StatusCodes.Status400BadRequest, "https_required");
                        return;
                    }
                }

                await next(context);
            }
        );
        app.UseDevelopmentSwagger();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapMoongateAdmin();
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        app.MapFallback(
                "/{**path}",
                context =>
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return Task.CompletedTask;
                }
            )
            .AllowAnonymous()
            .ExcludeFromDescription();
        await app.RunAsync();
    }
}
