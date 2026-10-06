using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Moongate.Admin.Contracts.V1;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Accounts;

public class AccountListEndpointTests
{
    [Theory]
    [InlineData(AccountType.Regular)]
    [InlineData(AccountType.GameMaster)]
    public async Task List_NonAdministrator_ForbiddenBeforeRpc(AccountType role)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        grpc.Authority.Role = role;
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/accounts")).StatusCode);
        Assert.Equal(0, grpc.Authority.ListCallCount);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/accounts", new { username = "New", password = "fixture-only" })).StatusCode
        );
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/accounts/8/revoke-sessions", null)).StatusCode);
        Assert.Equal(0, grpc.Authority.CreateCallCount);
    }

    [Fact]
    public async Task List_CursorAndDefaults_PreservesPagination()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        var first = await client.GetFromJsonAsync<JsonElement>("/api/accounts");
        Assert.Equal((uint)50, grpc.Authority.LastList?.PageSize);
        Assert.Equal(uint.MaxValue, first.GetProperty("nextAfterAccountId").GetUInt32());
        var last = await client.GetFromJsonAsync<JsonElement>("/api/accounts?pageSize=200&afterAccountId=4294967295");
        Assert.Equal((uint)200, grpc.Authority.LastList?.PageSize);
        Assert.Equal(uint.MaxValue, grpc.Authority.LastList?.AfterAccountId);
        Assert.Equal((uint)0, last.GetProperty("nextAfterAccountId").GetUInt32());
    }

    [Theory]
    [InlineData("pageSize=201")]
    [InlineData("pageSize=-1")]
    [InlineData("pageSize=bad")]
    [InlineData("afterAccountId=4294967296")]
    public async Task List_InvalidQuery_ReturnsBadRequest(string query)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        var response = await client.GetAsync("/api/accounts?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, grpc.Authority.ListCallCount);
    }

    [Fact]
    public async Task List_Anonymous_ReturnsUnauthorized()
    {
        await using var factory = new AdminApiFactory();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await AuthenticatedApiClient.Create(factory).GetAsync("/api/accounts")).StatusCode
        );
    }
}
