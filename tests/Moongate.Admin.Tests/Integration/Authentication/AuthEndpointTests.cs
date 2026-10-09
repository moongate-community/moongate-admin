using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
        var text = await response.Content.ReadAsStringAsync();
        var body = JsonDocument.Parse(text).RootElement;
        var token = body.GetProperty("accessToken").GetString();
        Assert.NotNull(token);
        Assert.Equal(3, token.Split('.').Length);
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());
        Assert.DoesNotContain(grpc.Authority.Token, text);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Theory]
    [InlineData("", "fixture-only")]
    [InlineData("   ", "fixture-only")]
    [InlineData("Admin", "")]
    [InlineData("Admin", "   ")]
    public async Task Login_BlankCredentials_RejectsBeforeUpstream(string user, string password)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var response = await AuthenticatedApiClient.Create(factory)
            .PostAsJsonAsync("/api/auth/login", new { username = user, password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, grpc.Authority.LoginCallCount);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("")]
    [InlineData("{\"username\":\"Admin\"}")]
    public async Task Login_MalformedBody_ReturnsBadRequest(string json)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var response = await AuthenticatedApiClient.Create(factory)
            .PostAsync("/api/auth/login", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, grpc.Authority.LoginCallCount);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.BadGateway)]
    public async Task Login_RejectedOrExpiredUpstream_DoesNotIssueJwt(bool expired, HttpStatusCode expected)
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
        var response = await AuthenticatedApiClient.Create(factory)
            .PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" });
        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain("accessToken", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_Unconfigured_ReturnsConfigurationRequired()
    {
        await using var factory = new AdminApiFactory();
        factory.Settings.Clear();
        var response = await AuthenticatedApiClient.Create(factory)
            .PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(
            "configuration_required",
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
        );
    }

    [Fact]
    public async Task Logout_UpstreamUnavailable_InvalidatesJwtLocally()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        grpc.Authority.LogoutFailure = StatusCode.Unavailable;
        var response = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("localSessionCleared").GetBoolean());
        grpc.Authority.LogoutFailure = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutSession_IsIdempotent()
    {
        await using var factory = new AdminApiFactory();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await AuthenticatedApiClient.Create(factory).PostAsync("/api/auth/logout", null)).StatusCode
        );
    }

    [Fact]
    public async Task Logout_ConcurrentSameToken_BothSucceed()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        var results = await Task.WhenAll(client.PostAsync("/api/auth/logout", null), client.PostAsync("/api/auth/logout", null));
        Assert.All(results, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        Assert.False(grpc.Authority.IsValid(grpc.Authority.Token));
    }
}
