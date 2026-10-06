using System.Net;
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
    public async Task Login_ValidCredentials_ReturnsSafeIdentityAndSecureCookie()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = AuthenticatedApiClient.Create(factory);
        await AuthenticatedApiClient.RefreshCsrfAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(grpc.Authority.Token, text);
        Assert.DoesNotContain("fixture-only", text);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-MoongateAdmin="));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(grpc.Authority.Token, cookie);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }
    [Fact]
    public async Task Login_MissingCsrf_FailsBeforeUpstream()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = AuthenticatedApiClient.Create(factory);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, grpc.Authority.LoginCallCount);
    }
    [Fact]
    public async Task Logout_UpstreamUnavailable_ClearsLocalSession()
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
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Host-MoongateAdmin=;") );
    }
    [Fact]
    public async Task Logout_WithoutSession_IsIdempotent()
    {
        await using var factory = new AdminApiFactory();
        var client = AuthenticatedApiClient.Create(factory);
        await AuthenticatedApiClient.RefreshCsrfAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
    }
    [Fact]
    public async Task Login_PreviousIdentityCsrf_IsRejected()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = AuthenticatedApiClient.Create(factory);
        await AuthenticatedApiClient.RefreshCsrfAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.False(grpc.Authority.Revoked);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.BadGateway)]
    public async Task Login_RejectedOrExpiredUpstream_DoesNotIssueCookie(bool expired, HttpStatusCode expected)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        if (expired)
        {
            grpc.Authority.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        }
        else
        {
            grpc.Authority.Failure = StatusCode.Unauthenticated;
        }
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = AuthenticatedApiClient.Create(factory);
        await AuthenticatedApiClient.RefreshCsrfAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" });
        Assert.Equal(expected, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var values) && values.Any(value => value.StartsWith("__Host-MoongateAdmin=")));
        Assert.DoesNotContain("upstream-private-detail", await response.Content.ReadAsStringAsync());
    }
}
