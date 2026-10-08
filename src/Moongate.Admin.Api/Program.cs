using Moongate.Admin.Api.Extensions;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Errors;
using Serilog.Events;
using Serilog;
using Microsoft.AspNetCore.Http.Features;

namespace Moongate.Admin.Api;

public class Program
{
    private const long MaximumConfigurationBodyBytes = 65536;

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

                    if (context.Request.Path.StartsWithSegments("/api/configuration"))
                    {
                        if (context.Request.ContentLength > MaximumConfigurationBodyBytes)
                        {
                            await ProblemResponses.WriteAsync(
                                context,
                                StatusCodes.Status413PayloadTooLarge,
                                "request_body_too_large"
                            );
                            return;
                        }

                        var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                        if (limit is { IsReadOnly: false })
                        {
                            limit.MaxRequestBodySize = MaximumConfigurationBodyBytes;
                        }
                    }
                }

                await next(context);
            }
        );
        app.UseMoongateFrontend();
        app.UseRouting();
        app.UseDevelopmentSwagger();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapMoongateAdmin();
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        await app.RunAsync();
    }
}
