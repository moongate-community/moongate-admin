using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Services.Upstream;
using Moongate.Admin.Tests.TestSupport.Grpc;
using LoginRequest = Moongate.Admin.Api.Data.Accounts.LoginRequest;

namespace Moongate.Admin.Tests.Integration.Upstream;

public class GrpcMoongateAdminClientTests
{
    [Fact]
    public async Task Calls_RealGrpc_PreservesWireContract()
    {
        await using var fixture = await AdminGrpcFixture.StartAsync();
        var services = new ServiceCollection();
        services.AddHttpClient("MoongateAdmin");
        await using var provider = services.BuildServiceProvider();
        using var client = new GrpcMoongateAdminClient(
            Options.Create(
                new MoongateOptions
                {
                    AuthenticationEndpointId = "login", AllowInsecureLoopback = true,
                    Endpoints = [new MoongateEndpointOptions { Id = "login", Label = "Login", Address = fixture.Address }]
                }
            ),
            provider.GetRequiredService<IHttpClientFactory>(),
            TimeProvider.System
        );
        var login = await client.LoginAsync(
            new LoginRequest { Username = " Admin ", Password = "fixture-only" },
            CancellationToken.None
        );
        Assert.Equal(" Admin ", fixture.Authority.LastLogin?.Username);
        var server = await client.GetServerInfoAsync("login", login.AccessToken, CancellationToken.None);
        Assert.Equal("18446744073709551615", server.UptimeSeconds);
        Assert.Equal("Bearer " + fixture.Authority.Token, fixture.Authority.LastAuthorization);
        Assert.InRange(fixture.Authority.LastDeadline - DateTime.UtcNow, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        var page = await client.ListAccountsAsync(0, 0, login.AccessToken, CancellationToken.None);
        Assert.Equal((uint)50, fixture.Authority.LastList?.PageSize);
        Assert.Equal(uint.MaxValue, page.NextAfterAccountId);
        await client.RevokeAccountSessionsAsync(8, login.AccessToken, CancellationToken.None);
        Assert.Equal((uint)8, fixture.Authority.LastRevokedAccount);
        var created = await client.CreateAccountAsync(
            new Api.Data.Accounts.CreateAccountRequest { Username = "New", Password = "fixture-only" },
            login.AccessToken,
            CancellationToken.None
        );
        Assert.Equal(Api.Types.Accounts.AdminAccountType.Regular, created.AccountType);
        Assert.False(created.CanAccessApi);
        await client.LogoutAsync(login.AccessToken, CancellationToken.None);
        Assert.True(fixture.Authority.Revoked);
    }
}
