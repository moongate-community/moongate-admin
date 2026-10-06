using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using System.Text;
using Grpc.Core;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Accounts;

public class AccountCreateEndpointTests
{
    [Fact]
    public async Task Create_Defaults_ReturnsCreatedWithoutLocation()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        var response = await client.PostAsJsonAsync("/api/accounts", new { username = " New ", password = "fixture-only" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("regular", body.GetProperty("accountType").GetString());
        Assert.False(body.GetProperty("canAccessApi").GetBoolean());
        Assert.Equal(" New ", grpc.Authority.LastCreate?.Username);
        Assert.False(grpc.Authority.LastCreate?.HasAccountType);
    }

    [Theory]
    [InlineData("{\"username\":\"New\",\"password\":\"fixture-only\",\"accountType\":2}")]
    [InlineData("{\"username\":\"New\",\"password\":\"fixture-only\",\"accountType\":\"unknown\"}")]
    [InlineData("{broken")]
    [InlineData("")]
    public async Task Create_InvalidJson_ReturnsBadRequest(string json)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        var response = await client.PostAsync("/api/accounts", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, grpc.Authority.CreateCallCount);
    }

    [Fact]
    public async Task Create_ResponseLost_ReportsUncertaintyWithoutRetry()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        grpc.Authority.LoseCreateResponse = true;
        var response = await client.PostAsJsonAsync(
            "/api/accounts",
            new { username = "CreatedOnce", password = "fixture-only" }
        );
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("mutationOutcomeUnknown").GetBoolean());
        Assert.Equal(1, grpc.Authority.CreateCallCount);
        grpc.Authority.LoseCreateResponse = false;
        var list = await client.GetFromJsonAsync<JsonElement>("/api/accounts");
        Assert.Equal("CreatedOnce", list.GetProperty("accounts")[0].GetProperty("username").GetString());
    }

    [Theory]
    [InlineData(StatusCode.AlreadyExists, HttpStatusCode.Conflict, false)]
    [InlineData(StatusCode.Unavailable, HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(StatusCode.PermissionDenied, HttpStatusCode.Forbidden, false)]
    public async Task Create_UpstreamFailure_MapsStatusAndUncertainty(
        StatusCode failure, HttpStatusCode expected, bool uncertain
    )
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        grpc.Authority.Failure = failure;
        var response = await client.PostAsJsonAsync("/api/accounts", new { username = "New", password = "fixture-only" });
        Assert.Equal(expected, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(uncertain, body.TryGetProperty("mutationOutcomeUnknown", out var value) && value.GetBoolean());
        Assert.Equal(1, grpc.Authority.CreateCallCount);
    }

    [Theory]
    [InlineData("regular")]
    [InlineData("gameMaster")]
    [InlineData("administrator")]
    public async Task Create_ExplicitRoleAndAccess_PreservesValues(string role)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        var response = await client.PostAsJsonAsync(
            "/api/accounts",
            new { username = "New", password = "fixture-only", accountType = role, canAccessApi = true }
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(role, body.GetProperty("accountType").GetString());
        Assert.True(body.GetProperty("canAccessApi").GetBoolean());
        Assert.True(grpc.Authority.LastCreate?.HasAccountType);
    }
}
