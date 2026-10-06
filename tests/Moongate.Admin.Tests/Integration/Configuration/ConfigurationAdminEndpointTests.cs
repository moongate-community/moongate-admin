using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Moongate.Admin.Api.Interfaces.Configuration;
using Moongate.Admin.Contracts.V1;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Configuration;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Configuration;

public class ConfigurationAdminEndpointTests
{
    [Theory]
    [InlineData(AccountType.Regular)]
    [InlineData(AccountType.GameMaster)]
    public async Task Configuration_NonAdministrator_IsForbidden(AccountType role)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync(new FakeAdminAuthority { Role = role });
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        using var client = await AuthenticatedApiClient.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/configuration")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/configuration", ConfigurationHttpFixtures.Candidate(grpc.Address))).StatusCode);
        Assert.Equal(0, grpc.Authority.ListCallCount);
    }

    [Fact]
    public async Task Configuration_Anonymous_IsUnauthorized()
    {
        await using var factory = new AdminApiFactory();
        using var client = AuthenticatedApiClient.Create(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/configuration")).StatusCode);
    }

    [Theory]
    [InlineData(null, 428)]
    [InlineData("*", 412)]
    [InlineData("\"stale\"", 412)]
    [InlineData("W/\"11111111111111111111111111111111\"", 412)]
    [InlineData("\"11111111111111111111111111111111\",\"22222222222222222222222222222222\"", 412)]
    public async Task Put_InvalidPrecondition_PreservesRevision(string? tag, int expected)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        using var client = await AuthenticatedApiClient.CreateAsync(factory);
        var original = factory.Services.GetRequiredService<IConnectionCatalogStore>().Current.Revision;
        if (tag is not null) { client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", tag); }
        var response = await client.PutAsJsonAsync("/api/configuration", ConfigurationHttpFixtures.Candidate(grpc.Address));
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal(original, factory.Services.GetRequiredService<IConnectionCatalogStore>().Current.Revision);
        Assert.False(File.Exists(factory.ConfigurationDirectory.FilePath));
    }

    [Fact]
    public async Task Put_MatchingETag_ActivatesAndRequiresNewLogin()
    {
        await using var first = await AdminGrpcFixture.StartAsync();
        await using var second = await AdminGrpcFixture.StartAsync(instanceId: "replacement");
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(first);
        using var client = await AuthenticatedApiClient.CreateAsync(factory);
        var read = await client.GetAsync("/api/configuration");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", read.Headers.ETag?.Tag);
        var saved = await client.PutAsJsonAsync("/api/configuration", ConfigurationHttpFixtures.Candidate(second.Address, "replacement"));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.NotEqual(read.Headers.ETag, saved.Headers.ETag);
        Assert.True((await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reauthenticationRequired").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/configuration")).StatusCode);
        using var next = await AuthenticatedApiClient.CreateAsync(factory);
        Assert.Equal("replacement", (await next.GetFromJsonAsync<JsonElement>("/api/servers/replacement")).GetProperty("instanceId").GetString());
    }

    [Theory]
    [InlineData(false, 403)]
    [InlineData(true, 401)]
    public async Task Configuration_UpstreamDowngradeOrRevocation_IsEnforced(bool revoked, int expected)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        using var client = await AuthenticatedApiClient.CreateAsync(factory);
        if (revoked) { grpc.Authority.Revoked = true; }
        else { grpc.Authority.Role = AccountType.GameMaster; }
        Assert.Equal(expected, (int)(await client.GetAsync("/api/configuration")).StatusCode);
        Assert.False(File.Exists(factory.ConfigurationDirectory.FilePath));
    }

    [Fact]
    public async Task Put_FileFailure_RedactsAndRetainsConfiguration()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        using var client = await AuthenticatedApiClient.CreateAsync(factory);
        var read = await client.GetAsync("/api/configuration");
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", read.Headers.ETag?.Tag);
        Directory.CreateDirectory(factory.ConfigurationDirectory.FilePath);
        var response = await client.PutAsJsonAsync("/api/configuration", ConfigurationHttpFixtures.Candidate(grpc.Address));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("configuration_save_failed", text);
        Assert.DoesNotContain(factory.ConfigurationDirectory.Root, text);
        Assert.Equal(read.Headers.ETag, (await client.GetAsync("/api/configuration")).Headers.ETag);
    }
}
