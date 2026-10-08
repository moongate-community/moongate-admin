using System.Net;
using Microsoft.AspNetCore.Hosting;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Hosting;

public class FrontendHostingTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/setup")]
    [InlineData("/login")]
    [InlineData("/servers/selected")]
    public async Task Frontend_AnonymousUiPath_ReturnsBuiltIndex(string path)
    {
        using var assets = new TemporaryFrontendDirectory();
        await using var factory = new AdminApiFactory { EnvironmentName = "Production" };
        await using var host = factory.WithWebHostBuilder(builder => builder.UseWebRoot(assets.Root));
        using var client = host.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("frontend-fixture", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/missing")]
    [InlineData("/health/missing")]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/assets/missing.js")]
    [InlineData("/assets/missing")]
    [InlineData("/missing.png")]
    public async Task Frontend_ReservedOrMissingAsset_ReturnsRealNotFound(string path)
    {
        using var assets = new TemporaryFrontendDirectory();
        await using var factory = new AdminApiFactory { EnvironmentName = "Production" };
        await using var host = factory.WithWebHostBuilder(builder => builder.UseWebRoot(assets.Root));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("frontend-fixture", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Frontend_BuiltAsset_ReturnsJavaScript()
    {
        using var assets = new TemporaryFrontendDirectory();
        await using var factory = new AdminApiFactory();
        await using var host = factory.WithWebHostBuilder(builder => builder.UseWebRoot(assets.Root));
        using var client = host.CreateClient();
        var response = await client.GetAsync("/assets/fixture.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("window.frontendFixture", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Frontend_NoBuild_PreservesApiAndReturnsNotFound()
    {
        using var assets = new TemporaryFrontendDirectory(false);
        await using var factory = new AdminApiFactory();
        await using var host = factory.WithWebHostBuilder(builder => builder.UseWebRoot(assets.Root));
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public async Task Frontend_ApiRequests_PreserveHttpsAndAuthorizationGuards()
    {
        using var assets = new TemporaryFrontendDirectory();
        await using var factory = new AdminApiFactory();
        await using var host = factory.WithWebHostBuilder(builder => builder.UseWebRoot(assets.Root));
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("http://localhost/api/configuration/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("https://localhost/api/servers")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/setup", null)).StatusCode);
    }
}
