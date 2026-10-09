using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moongate.Admin.Tests.TestSupport.Logging;
using Serilog.Core;

namespace Moongate.Admin.Tests.TestSupport.Hosting;

public class AdminApiFactory : WebApplicationFactory<Program>
{
    public Dictionary<string, string?> Settings { get; } = new()
    {
        ["Moongate:AuthenticationEndpointId"] = "login",
        ["Moongate:Endpoints:0:Id"] = "login",
        ["Moongate:Endpoints:0:Label"] = "Login",
        ["Moongate:Endpoints:0:Address"] = "https://127.0.0.1:2590"
    };

    public MemoryLogSink Logs { get; } = new();
    public TimeProvider Clock { get; set; } = TimeProvider.System;
    public string EnvironmentName { get; set; } = "Development";
    public Func<HttpMessageHandler>? GrpcHandler { get; set; }

    public void UseGrpc(Grpc.AdminGrpcFixture fixture)
    {
        Settings["Moongate:Endpoints:0:Address"] = fixture.Address;
        Settings["Moongate:AllowInsecureLoopback"] = "true";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILogEventSink>(Logs);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(Clock);
                if (GrpcHandler is not null)
                {
                    services.AddHttpClient("MoongateAdmin").ConfigurePrimaryHttpMessageHandler(GrpcHandler);
                }
            }
        );
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(Settings));
    }
}
