using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
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

    [Theory]
    [InlineData("https://user:pass@host:2590")]
    [InlineData("https://host:2590/?query=true")]
    [InlineData("https://host:2590/#fragment")]
    [InlineData("ftp://host:2590")]
    [InlineData("http://remote:2590")]
    public void Validate_UnsafeAddress_Fails(string address)
    {
        using var factory = new AdminApiFactory();
        factory.Settings["Moongate:Endpoints:0:Address"] = address;
        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }

    [Fact]
    public void Validate_MissingAuthenticationEndpoint_Fails()
    {
        using var factory = new AdminApiFactory();
        factory.Settings["Moongate:AuthenticationEndpointId"] = "missing";
        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }

    [Fact]
    public void Validate_DuplicateEndpointId_Fails()
    {
        using var factory = new AdminApiFactory();
        factory.Settings["Moongate:Endpoints:1:Id"] = "login";
        factory.Settings["Moongate:Endpoints:1:Label"] = "Other";
        factory.Settings["Moongate:Endpoints:1:Address"] = "https://127.0.0.1:2591";
        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }
}
