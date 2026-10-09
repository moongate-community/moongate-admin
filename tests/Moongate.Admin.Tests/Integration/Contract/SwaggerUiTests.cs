using System.Net;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Contract;

public class SwaggerUiTests
{
    [Fact]
    public async Task Swagger_Development_ServesUiAndAssets()
    {
        await using var factory = new AdminApiFactory();
        var client = AuthenticatedApiClient.Create(factory);
        var page = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("swagger-ui", await page.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/swagger-ui-bundle.js")).StatusCode);
        var script = await (await client.GetAsync("/swagger/index.js")).Content.ReadAsStringAsync();
        Assert.Contains("openapi/v1.json", script);
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/index.js")]
    public async Task Swagger_Production_IsUnavailable(string path)
    {
        await using var factory = new AdminApiFactory { EnvironmentName = "Production" };
        Assert.Equal(HttpStatusCode.NotFound, (await AuthenticatedApiClient.Create(factory).GetAsync(path)).StatusCode);
    }
}
