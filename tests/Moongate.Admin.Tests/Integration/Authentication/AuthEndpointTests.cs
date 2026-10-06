using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Grpc.Core;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Authentication;

public class AuthEndpointTests
{
    [Fact]
    public async Task Login_ValidCredentials_ReturnsJwtWithoutUpstreamTokenOrCookie()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = AuthenticatedApiClient.Create(factory);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString();
        Assert.NotNull(token);
        Assert.Equal(3, token.Split('.').Length);
        Assert.DoesNotContain(grpc.Authority.Token, await response.Content.ReadAsStringAsync());
        Assert.False(response.Headers.Contains("Set-Cookie"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
    }
    [Fact]
    public async Task Logout_UpstreamUnavailable_InvalidatesJwtLocally()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        grpc.Authority.Failure = StatusCode.Unavailable;
        var response = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("localSessionCleared").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
    }
    [Fact]
    public async Task Logout_WithoutSession_IsIdempotent()
    {
        await using var factory = new AdminApiFactory();
        Assert.Equal(HttpStatusCode.NoContent, (await AuthenticatedApiClient.Create(factory).PostAsync("/api/auth/logout", null)).StatusCode);
    }
    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.BadGateway)]
    public async Task Login_RejectedOrExpiredUpstream_DoesNotIssueJwt(bool expired, HttpStatusCode expected)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        if (expired) grpc.Authority.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        else grpc.Authority.Failure = StatusCode.Unauthenticated;
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var response = await AuthenticatedApiClient.Create(factory).PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" });
        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain("accessToken", await response.Content.ReadAsStringAsync());
    }
}
