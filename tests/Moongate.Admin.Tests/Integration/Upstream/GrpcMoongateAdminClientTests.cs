using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moongate.Admin.Api.Data.Accounts;
using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Upstream;
using Moongate.Admin.Api.Types.Accounts;
using Moongate.Admin.Tests.TestSupport.Grpc;

namespace Moongate.Admin.Tests.Integration.Upstream;

public class GrpcMoongateAdminClientTests
{
    private static (GrpcMoongateAdminClient Client, ServiceProvider Provider) Create(AdminGrpcFixture fixture)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("MoongateAdmin");
        var provider = services.BuildServiceProvider();
        var client = new GrpcMoongateAdminClient(
            new MoongateOptions
            {
                AuthenticationEndpointId = "login", AllowInsecureLoopback = true,
                Endpoints = [new MoongateEndpointOptions { Id = "login", Label = "Login", Address = fixture.Address }]
            },
            provider.GetRequiredService<IHttpClientFactory>(),
            TimeProvider.System
        );
        return (client, provider);
    }

    [Fact]
    public async Task Calls_RealGrpc_PreservesWireContract()
    {
        await using var fixture = await AdminGrpcFixture.StartAsync();
        var (client, provider) = Create(fixture);
        using var _ = client;
        await using var __ = provider;
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
            new CreateAccountRequest { Username = "New", Password = "fixture-only" },
            login.AccessToken,
            CancellationToken.None
        );
        Assert.Equal(AdminAccountType.Regular, created.AccountType);
        Assert.False(created.CanAccessApi);
        await client.LogoutAsync(login.AccessToken, CancellationToken.None);
        Assert.False(fixture.Authority.IsValid(login.AccessToken));
    }

    [Fact]
    public async Task Login_ExpiredUpstreamSession_Rejects()
    {
        await using var fixture = await AdminGrpcFixture.StartAsync(
            new FakeAdminAuthority { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) }
        );
        var (client, provider) = Create(fixture);
        using var _ = client;
        await using var __ = provider;
        var error = await Assert.ThrowsAsync<UpstreamCallException>(() => client.LoginAsync(
                new LoginRequest { Username = "Admin", Password = "fixture-only" },
                CancellationToken.None
            )
        );
        Assert.Equal(StatusCode.Internal, error.StatusCode);
    }

    [Fact]
    public async Task UnknownServerId_IsNotFound_AndUnconfiguredIsReported()
    {
        await using var fixture = await AdminGrpcFixture.StartAsync();
        var (client, provider) = Create(fixture);
        using var _ = client;
        await using var __ = provider;
        var missing = await Assert.ThrowsAsync<BadHttpRequestException>(() =>
            client.GetServerInfoAsync("nope", "token", CancellationToken.None)
        );
        Assert.Equal(404, missing.StatusCode);
        using var empty = new GrpcMoongateAdminClient(
            new MoongateOptions(),
            provider.GetRequiredService<IHttpClientFactory>(),
            TimeProvider.System
        );
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => empty.LoginAsync(
                new LoginRequest { Username = "Admin", Password = "fixture-only" },
                CancellationToken.None
            )
        );
        Assert.Equal("configuration_required", error.Code);
    }

    [Fact]
    public async Task Create_LostOrMalformedResponse_FlagsUnknownOutcomeWithoutRetry()
    {
        var authority = new FakeAdminAuthority { LoseCreateResponse = true };
        await using var fixture = await AdminGrpcFixture.StartAsync(authority);
        var (client, provider) = Create(fixture);
        using var _ = client;
        await using var __ = provider;
        var login = await client.LoginAsync(
            new LoginRequest { Username = "Admin", Password = "fixture-only" },
            CancellationToken.None
        );
        var lost = await Assert.ThrowsAsync<UpstreamCallException>(() => client.CreateAccountAsync(
                new CreateAccountRequest { Username = "New", Password = "fixture-only" },
                login.AccessToken,
                CancellationToken.None
            )
        );
        Assert.True(lost.MutationOutcomeUnknown);
        Assert.Equal(1, authority.CreateCallCount);
        authority.LoseCreateResponse = false;
        authority.MalformedCreateResponse = true;
        var malformed = await Assert.ThrowsAsync<UpstreamCallException>(() => client.CreateAccountAsync(
                new CreateAccountRequest { Username = "New", Password = "fixture-only" },
                login.AccessToken,
                CancellationToken.None
            )
        );
        Assert.True(malformed.MutationOutcomeUnknown);
        Assert.Equal(StatusCode.Internal, malformed.StatusCode);
        Assert.Equal(2, authority.CreateCallCount);
    }
}
