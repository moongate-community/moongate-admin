using System.Net;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Accounts;

public class AccountSessionEndpointTests
{
    [Theory]
    [InlineData("0", HttpStatusCode.BadRequest)]
    [InlineData("bad", HttpStatusCode.BadRequest)]
    [InlineData("4294967296", HttpStatusCode.BadRequest)]
    [InlineData("999", HttpStatusCode.NotFound)]
    [InlineData("8", HttpStatusCode.NoContent)]
    public async Task Revoke_Id_MapsResult(string id, HttpStatusCode expected)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        var response = await client.PostAsync("/api/accounts/" + id + "/revoke-sessions", null);
        Assert.Equal(expected, response.StatusCode);
    }
    [Fact]
    public async Task Revoke_Self_ClearsCurrentSession()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/accounts/7/revoke-sessions", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
    }
    [Fact]
    public async Task Revoke_MissingCsrf_FailsBeforeMutation()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/accounts/8/revoke-sessions", null)).StatusCode);
        Assert.Equal((uint)0, grpc.Authority.LastRevokedAccount);
    }
}
