using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moongate.Admin.Contracts.V1;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Servers;

public class ServerEndpointTests
{
    [Theory]
    [InlineData(AccountType.Regular, ServerMode.Login, "login")]
    [InlineData(AccountType.GameMaster, ServerMode.Game, "game")]
    [InlineData(AccountType.Administrator, ServerMode.Standalone, "standalone")]
    public async Task Reads_ValidRole_ReturnsConfiguredServer(AccountType role, ServerMode mode, string expectedMode)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        grpc.Authority.Role = role;
        grpc.Authority.Mode = mode;
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/servers");
        Assert.Equal("login", list[0].GetProperty("id").GetString());
        Assert.False(list[0].TryGetProperty("address", out _));
        var server = await client.GetFromJsonAsync<JsonElement>("/api/servers/login");
        Assert.Equal("fixture-login", server.GetProperty("instanceId").GetString());
        Assert.Equal(expectedMode, server.GetProperty("mode").GetString());
        Assert.Equal("18446744073709551615", server.GetProperty("uptimeSeconds").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/servers/missing")).StatusCode);
    }
    [Theory]
    [InlineData("/api/auth/session")]
    [InlineData("/api/servers")]
    [InlineData("/api/servers/login")]
    public async Task Read_Anonymous_ReturnsUnauthorized(string path)
    {
        await using var factory = new AdminApiFactory();
        var response = await AuthenticatedApiClient.Create(factory).GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }
}
