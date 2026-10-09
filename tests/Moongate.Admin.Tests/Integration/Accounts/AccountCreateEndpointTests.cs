using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Grpc.Core;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Accounts;

public class AccountCreateEndpointTests
{
    private static async Task<(AdminGrpcFixture, AdminApiFactory, HttpClient)> StartAsync()
    {
        var grpc = await AdminGrpcFixture.StartAsync();
        var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        return (grpc, factory, await AuthenticatedApiClient.CreateAsync(factory));
    }

    [Fact]
    public async Task Create_Defaults_ReturnsCreatedWithoutLocation()
    {
        var (grpc, factory, client) = await StartAsync();
        await using var _ = grpc;
        await using var __ = factory;
        var response = await client.PostAsJsonAsync("/api/accounts", new { username = " New ", password = "fixture-only" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("regular", body.GetProperty("accountType").GetString());
        Assert.False(body.GetProperty("canAccessApi").GetBoolean());
        Assert.Equal(" New ", grpc.Authority.LastCreate?.Username);
        Assert.False(grpc.Authority.LastCreate?.HasAccountType);
    }

    [Fact]
    public async Task Create_ExplicitNullRole_MeansRegular()
    {
        var (grpc, factory, client) = await StartAsync();
        await using var _ = grpc;
        await using var __ = factory;
        var response = await client.PostAsync(
            "/api/accounts",
            new StringContent(
                "{\"username\":\"New\",\"password\":\"fixture-only\",\"accountType\":null}",
                Encoding.UTF8,
                "application/json"
            )
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(grpc.Authority.LastCreate?.HasAccountType);
    }

    [Theory]
    [InlineData("{\"username\":\"New\",\"password\":\"fixture-only\",\"accountType\":2}")]
    [InlineData("{\"username\":\"New\",\"password\":\"fixture-only\",\"accountType\":\"unknown\"}")]
    [InlineData("{\"username\":\"New\",\"password\":\"fixture-only\",\"accountType\":\"regular, gameMaster\"}")]
    [InlineData("{\"username\":\" \",\"password\":\"fixture-only\"}")]
    [InlineData("{\"username\":\"New\",\"password\":\"  \"}")]
    [InlineData("{broken")]
    [InlineData("")]
    public async Task Create_InvalidInput_ReturnsBadRequestBeforeRpc(string json)
    {
        var (grpc, factory, client) = await StartAsync();
        await using var _ = grpc;
        await using var __ = factory;
        var response = await client.PostAsync("/api/accounts", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, grpc.Authority.CreateCallCount);
    }

    [Theory]
    [InlineData("regular")]
    [InlineData("gameMaster")]
    [InlineData("administrator")]
    public async Task Create_ExplicitRoleAndAccess_PreservesValues(string role)
    {
        var (grpc, factory, client) = await StartAsync();
        await using var _ = grpc;
        await using var __ = factory;
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

    [Fact]
    public async Task Create_ResponseLost_ReportsUncertaintyWithoutRetry()
    {
        var (grpc, factory, client) = await StartAsync();
        await using var _ = grpc;
        await using var __ = factory;
        grpc.Authority.LoseCreateResponse = true;
        var response = await client.PostAsJsonAsync("/api/accounts", new { username = "CreatedOnce", password = "fixture-only" });
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mutationOutcomeUnknown").GetBoolean());
        Assert.Equal(1, grpc.Authority.CreateCallCount);
        grpc.Authority.LoseCreateResponse = false;
        var list = await client.GetFromJsonAsync<JsonElement>("/api/accounts");
        Assert.Equal("CreatedOnce", list.GetProperty("accounts")[0].GetProperty("username").GetString());
    }

    [Fact]
    public async Task Create_MalformedSuccess_ReportsUncertaintyWithoutRetry()
    {
        var (grpc, factory, client) = await StartAsync();
        await using var _ = grpc;
        await using var __ = factory;
        grpc.Authority.MalformedCreateResponse = true;
        var response = await client.PostAsJsonAsync("/api/accounts", new { username = "CreatedOnce", password = "fixture-only" });
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mutationOutcomeUnknown").GetBoolean());
        Assert.Equal(1, grpc.Authority.CreateCallCount);
    }

    [Theory]
    [InlineData(StatusCode.AlreadyExists, HttpStatusCode.Conflict, false)]
    [InlineData(StatusCode.Unavailable, HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(StatusCode.PermissionDenied, HttpStatusCode.Forbidden, false)]
    public async Task Create_UpstreamFailure_MapsStatusAndUncertainty(StatusCode failure, HttpStatusCode expected, bool uncertain)
    {
        var (grpc, factory, client) = await StartAsync();
        await using var _ = grpc;
        await using var __ = factory;
        grpc.Authority.Failure = failure;
        var response = await client.PostAsJsonAsync("/api/accounts", new { username = "New", password = "fixture-only" });
        Assert.Equal(expected, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(uncertain, body.TryGetProperty("mutationOutcomeUnknown", out var value) && value.GetBoolean());
        Assert.Equal(1, grpc.Authority.CreateCallCount);
        Assert.DoesNotContain("upstream-private-detail", body.ToString());
    }
}
