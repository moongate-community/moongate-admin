using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moongate.Admin.Contracts.V1;

namespace Moongate.Admin.Tests.TestSupport.Grpc;

public sealed class AdminGrpcFixture : IAsyncDisposable
{
    private readonly WebApplication _app;
    public FakeAdminAuthority Authority { get; }
    public string Address { get; private set; } = "";

    private AdminGrpcFixture(WebApplication app, FakeAdminAuthority authority)
    {
        _app = app;
        Authority = authority;
    }

    public static async Task<AdminGrpcFixture> StartAsync(
        FakeAdminAuthority? authority = null, X509Certificate2? certificate = null,
        ServerMode? mode = null, string? instanceId = null
    )
    {
        authority ??= new FakeAdminAuthority();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(
                IPAddress.Loopback,
                0,
                listen =>
                {
                    listen.Protocols = HttpProtocols.Http2;
                    if (certificate is not null)
                    {
                        listen.UseHttps(certificate);
                    }
                }
            )
        );
        builder.Services.AddGrpc();
        builder.Services.AddSingleton(authority);
        var state = new FakeAdminHostState { Mode = mode ?? authority.Mode, InstanceId = instanceId ?? authority.InstanceId };
        builder.Services.AddSingleton(state);
        var app = builder.Build();
        if (state.Mode is ServerMode.Login or ServerMode.Standalone)
        {
            app.MapGrpcService<FakeAdminLoginService>();
            app.MapGrpcService<FakeAdminAccountsService>();
            app.MapGrpcService<FakeAdminAccountSessionsService>();
        }

        app.MapGrpcService<FakeAdminSessionService>();
        app.MapGrpcService<FakeAdminServerService>();
        await app.StartAsync();
        return new AdminGrpcFixture(app, authority)
        {
            Address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.Single()
                      ?? throw new InvalidOperationException("Missing fixture address.")
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
    }
}
