using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Moongate.Admin.Tests.TestSupport.Logging;
using Serilog.Core;
using ApiProgram = Moongate.Admin.Api.Program;

namespace Moongate.Admin.Tests.TestSupport.Hosting;

public class AdminApiFactory : WebApplicationFactory<ApiProgram>
{
    public Dictionary<string, string?> Settings { get; } = new()
    {
        ["Moongate:AuthenticationEndpointId"] = "login",
        ["Moongate:Endpoints:0:Id"] = "login",
        ["Moongate:Endpoints:0:Label"] = "Login",
        ["Moongate:Endpoints:0:Address"] = "https://127.0.0.1:2590"
    };

    public void UseGrpc(Grpc.AdminGrpcFixture fixture)
    {
        Settings["Moongate:Endpoints:0:Address"] = fixture.Address;
        Settings["Moongate:AllowInsecureLoopback"] = "true";
    }

    public MemoryLogSink Logs { get; } = new();
    public TimeProvider Clock { get; set; } = TimeProvider.System;
    public string EnvironmentName { get; set; } = "Development";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILogEventSink>(Logs);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(Clock);
            }
        );
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(Settings));
    }
}
