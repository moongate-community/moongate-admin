using System.Text.Json;
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
