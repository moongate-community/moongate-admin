using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Grpc.Core;
using Microsoft.Extensions.Time.Testing;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Authentication;

public class SessionEndpointTests
{
    [Fact]
    public async Task Read_RevokedToken_ReturnsUnauthorizedAndInvalidatesJwt()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
        grpc.Authority.RevokeAll();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
        grpc.Authority.IssueToken();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
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
    public async Task Read_JwtFromAnotherInstance_ReturnsUnauthorized()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var first = new AdminApiFactory();
        first.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(first);
        await using var second = new AdminApiFactory();
        second.UseGrpc(grpc);
        var other = AuthenticatedApiClient.Create(second);
        other.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task Login_Replacement_InvalidatesPreviousJwt()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        var previous = client.DefaultRequestHeaders.Authorization;
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            body.GetProperty("accessToken").GetString()
        );
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
        var old = AuthenticatedApiClient.Create(factory);
        old.DefaultRequestHeaders.Authorization = previous;
        Assert.Equal(HttpStatusCode.Unauthorized, (await old.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task Login_FailedWhileSignedIn_KeepsPreviousSession()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        grpc.Authority.Failure = StatusCode.Unauthenticated;
        var failed = await client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        grpc.Authority.Failure = null;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
    }
}
