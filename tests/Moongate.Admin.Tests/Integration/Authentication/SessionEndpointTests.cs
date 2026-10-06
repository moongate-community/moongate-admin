using System.Net.Http.Json;
using System.Net;
using Grpc.Core;
using Microsoft.Extensions.Time.Testing;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Authentication;

public class SessionEndpointTests
{
    [Theory]
    [InlineData("/api/auth/session")]
    [InlineData("/api/servers")]
    [InlineData("/api/servers/login")]
    public async Task Read_RevokedToken_ReturnsUnauthorizedAndClearsCookie(string path)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        grpc.Authority.RevokeIssuedToken();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Host-MoongateAdmin=;"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Read_Outage_PreservesSessionForRecovery()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        grpc.Authority.Failure = StatusCode.Unavailable;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/auth/session")).StatusCode);
        grpc.Authority.Failure = null;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task Read_AtAbsoluteExpiry_ReturnsUnauthorized()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var grpc = await AdminGrpcFixture.StartAsync();
        grpc.Authority.ExpiresAt = clock.GetUtcNow().AddMinutes(30);
        await using var factory = new AdminApiFactory { Clock = clock };
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
        clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task Read_CookieFromAnotherInstance_ReturnsUnauthorized()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var first = new AdminApiFactory();
        first.UseGrpc(grpc);
        var client = AuthenticatedApiClient.Create(first);
        await AuthenticatedApiClient.RefreshCsrfAsync(client);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" });
        var cookie = login.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("__Host-MoongateAdmin="))
            .Split(';')[0];
        await using var second = new AdminApiFactory();
        second.UseGrpc(grpc);
        var other = AuthenticatedApiClient.Create(second);
        other.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task Login_Replacement_InvalidatesPreviousCookie()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = AuthenticatedApiClient.Create(factory);
        await AuthenticatedApiClient.RefreshCsrfAsync(client);
        var first = await client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" });
        var oldCookie = first.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("__Host-MoongateAdmin="))
            .Split(';')[0];
        await AuthenticatedApiClient.RefreshCsrfAsync(client);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" }))
            .StatusCode
        );
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
        var oldClient = AuthenticatedApiClient.Create(factory);
        oldClient.DefaultRequestHeaders.Add("Cookie", oldCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldClient.GetAsync("/api/auth/session")).StatusCode);
    }
}
