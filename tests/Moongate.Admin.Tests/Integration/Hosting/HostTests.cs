using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Hosting;

public class HostTests
{
    [Fact]
    public async Task Live_WithoutUpstream_ReturnsOk()
    {
        await using var factory = new AdminApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Start_EmptyConfiguration_ServesLivenessWithoutContactingUpstream()
    {
        await using var factory = new AdminApiFactory();
        factory.Settings.Clear();
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public void Start_InvalidConfiguration_FailsStartup()
    {
        var factory = new AdminApiFactory();
        factory.Settings["Moongate:AuthenticationEndpointId"] = "missing";
        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        factory.Dispose();
    }
}
