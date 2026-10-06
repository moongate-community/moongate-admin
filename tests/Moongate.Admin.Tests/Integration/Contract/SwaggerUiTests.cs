using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        var initialization = await client.GetAsync("/swagger/index.js");
        Assert.Equal(HttpStatusCode.OK, initialization.StatusCode);
        var script = await initialization.Content.ReadAsStringAsync();
        Assert.Contains("openapi/v1.json", script);
        Assert.DoesNotContain("X-CSRF-TOKEN", script);
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/index.js")]
    [InlineData("/swagger/swagger-ui-bundle.js")]
    public async Task Swagger_Production_IsUnavailable(string path)
    {
        await using var factory = new AdminApiFactory { EnvironmentName = "Production" };
        Assert.Equal(HttpStatusCode.NotFound, (await AuthenticatedApiClient.Create(factory).GetAsync(path)).StatusCode);
    }
    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/logout")]
    public async Task Swagger_OptionalAuthentication_AttachesAuthorizedBearer(string path)
    {
        await using var factory = new AdminApiFactory();
        var document = await AuthenticatedApiClient.Create(factory).GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var security = document.GetProperty("paths").GetProperty(path).GetProperty("post").GetProperty("security");
        Assert.Contains(security.EnumerateArray(), requirement => requirement.TryGetProperty("AdminBearer", out _));
        Assert.Contains(security.EnumerateArray(), requirement => !requirement.EnumerateObject().Any());
    }
}
