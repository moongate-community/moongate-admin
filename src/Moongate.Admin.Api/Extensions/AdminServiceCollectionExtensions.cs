using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Moongate.Admin.Api.Services.Authentication;
using Moongate.Admin.Api.Types.Authentication;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Services.Config;
using Moongate.Admin.Api.Interfaces.Upstream;
using Moongate.Admin.Api.Services.Upstream;
using Moongate.Admin.Api.Services.Errors;

namespace Moongate.Admin.Api.Extensions;

public static class AdminServiceCollectionExtensions
{
    public static IServiceCollection AddMoongateAdmin(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton<IValidateOptions<MoongateOptions>>(new MoongateOptionsValidator(environment));
        services.AddOptions<MoongateOptions>().Bind(configuration.GetSection("Moongate")).ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<MemoryTicketStore>();
        services.AddScoped<AdminSessionAccessor>();
        services.AddScoped<AdminCookieEvents>();
        services.AddAuthentication(AdminAuthentication.Scheme).AddCookie(AdminAuthentication.Scheme, options =>
        {
            options.Cookie.Name = AdminAuthentication.Cookie;
            options.Cookie.Path = "/";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.SlidingExpiration = false;
            options.EventsType = typeof(AdminCookieEvents);
        });
        services.AddOptions<CookieAuthenticationOptions>(AdminAuthentication.Scheme).Configure<MemoryTicketStore, TimeProvider>((options, store, clock) =>
        {
            options.SessionStore = store;
            options.TimeProvider = clock;
        });
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy(AdminAuthentication.AccountPolicy, policy => policy.RequireAuthenticatedUser().RequireRole("administrator"));
        });
        services.AddAntiforgery(options =>
        {
            options.HeaderName = AdminAuthentication.CsrfHeader;
            options.Cookie.Name = AdminAuthentication.CsrfCookie;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
        services.AddProblemDetails();
        services.AddExceptionHandler<AdminApiExceptionHandler>();
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.AddHttpClient("MoongateAdmin").ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddSingleton<IMoongateAdminClient, GrpcMoongateAdminClient>();
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
        return services;
    }
}
