using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Interfaces.Upstream;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Authentication;
using Moongate.Admin.Api.Services.Config;
using Moongate.Admin.Api.Services.Errors;
using Moongate.Admin.Api.Services.Serialization;
using Moongate.Admin.Api.Services.Upstream;
using Moongate.Admin.Api.Types.Authentication;

namespace Moongate.Admin.Api.Extensions;

public static class AdminServiceCollectionExtensions
{
    public static IServiceCollection AddMoongateAdmin(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment
    )
    {
        services.AddSingleton<IValidateOptions<MoongateOptions>, MoongateOptionsValidator>();
        services.AddOptions<MoongateOptions>().Bind(configuration.GetSection("Moongate")).ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient("MoongateAdmin")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddSingleton<IMoongateAdminClient>(provider => new GrpcMoongateAdminClient(
                provider.GetRequiredService<IOptions<MoongateOptions>>().Value,
                provider.GetRequiredService<IHttpClientFactory>(),
                provider.GetRequiredService<TimeProvider>()
            )
        );
        services.AddSingleton<JwtSessionService>();
        services.AddAuthentication(AdminAuthentication.Scheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(AdminAuthentication.Scheme)
            .Configure<JwtSessionService>((options, sessions) =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = sessions.CreateValidationParameters();
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            var session = sessions.Find(context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value);
                            if (session is null)
                            {
                                context.Fail("Invalid administration session.");
                            }
                            else
                            {
                                context.HttpContext.Items[AdminAuthentication.SessionItem] = session;
                            }

                            return Task.CompletedTask;
                        },
                        OnChallenge = context =>
                        {
                            context.HandleResponse();
                            context.Response.Headers.WWWAuthenticate = "Bearer";
                            return ProblemResponses.WriteAsync(
                                context.HttpContext,
                                StatusCodes.Status401Unauthorized,
                                "authentication_required"
                            );
                        },
                        OnForbidden = context => ProblemResponses.WriteAsync(
                            context.HttpContext,
                            StatusCodes.Status403Forbidden,
                            "permission_denied"
                        )
                    };
                }
            );
        services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                options.AddPolicy(
                    AdminAuthentication.AccountPolicy,
                    policy => policy.RequireAuthenticatedUser().RequireRole("administrator")
                );
            }
        );
        services.AddProblemDetails();
        services.AddExceptionHandler<AdminApiExceptionHandler>();
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.RespectNullableAnnotations = true;
                options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
                options.SerializerOptions.Converters.Add(new AdminAccountTypeJsonConverter());
                options.SerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
                );
            }
        );
        return services;
    }
}
