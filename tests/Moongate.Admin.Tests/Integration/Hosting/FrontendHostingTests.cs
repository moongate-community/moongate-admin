using System.Net;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Hosting;

public class FrontendHostingTests
{
    private static AdminApiFactory Create(TemporaryFrontendDirectory directory)
    {
        var factory = new AdminApiFactory();
        factory.Settings["Frontend:Path"] = directory.Root;
        return factory;
    }

    [Fact]
    public async Task Root_ServesIndexWithNoStore()
    {
        using var directory = new TemporaryFrontendDirectory();
        await using var factory = Create(directory);
        var response = await AuthenticatedApiClient.Create(factory).GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("spa-index-marker", await response.Content.ReadAsStringAsync());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task DeepLink_FallsBackToIndex()
    {
        using var directory = new TemporaryFrontendDirectory();
        await using var factory = Create(directory);
        var response = await AuthenticatedApiClient.Create(factory).GetAsync("/servers/login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("spa-index-marker", await response.Content.ReadAsStringAsync());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Asset_IsServedAndMissingAssetIsNotFound()
    {
        using var directory = new TemporaryFrontendDirectory();
        await using var factory = Create(directory);
        var client = AuthenticatedApiClient.Create(factory);
        var asset = await client.GetAsync("/assets/app.js");
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Contains("asset-marker", await asset.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/assets/missing.js")).StatusCode);
    }

    [Theory]
    [InlineData("/api/nope")]
    [InlineData("/health/nope")]
    [InlineData("/swagger/nope")]
    [InlineData("/openapi/nope")]
    public async Task ReservedPrefixes_NeverFallBackToIndex(string path)
    {
        using var directory = new TemporaryFrontendDirectory();
        await using var factory = Create(directory);
        var response = await AuthenticatedApiClient.Create(factory).GetAsync(path);
        Assert.DoesNotContain("spa-index-marker", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NonGetRequest_DoesNotFallBackToIndex()
    {
        using var directory = new TemporaryFrontendDirectory();
        await using var factory = Create(directory);
        var response = await AuthenticatedApiClient.Create(factory).PostAsync("/servers", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApiAndHealth_StillWorkWithTheFrontend()
    {
        using var directory = new TemporaryFrontendDirectory();
        await using var factory = Create(directory);
        var client = AuthenticatedApiClient.Create(factory);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/servers")).StatusCode);
    }

    [Fact]
    public async Task MissingDirectory_LeavesBehaviorUnchanged()
    {
        await using var factory = new AdminApiFactory();
        var client = AuthenticatedApiClient.Create(factory);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/servers")).StatusCode);
    }
}
