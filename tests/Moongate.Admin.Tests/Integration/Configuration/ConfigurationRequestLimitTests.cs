using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Moongate.Admin.Tests.TestSupport.Configuration;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Configuration;

public class ConfigurationRequestLimitTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Setup_OversizedBody_RejectsBeforeBinding(bool chunked)
    {
        using var certificates = new TestGrpcCertificates();
        await using var factory = ConfigurationHttpFixtures.Unconfigured();
        factory.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listener => listener.UseHttps(certificates.Server)));
        using var initializer = factory.CreateClient();
        var address = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.Single()
            ?? throw new InvalidOperationException("Missing HTTPS listener.");
        using var client = new HttpClient(certificates.TrustedHandler()) { BaseAddress = new UriBuilder(address) { Host = "localhost" }.Uri };
        client.DefaultRequestHeaders.Add("X-Moongate-Setup-Token", ConfigurationHttpFixtures.SetupToken);
        var json = JsonSerializer.Serialize(ConfigurationHttpFixtures.Candidate("https://localhost:2590"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using HttpContent content = chunked ? new ChunkedJsonContent(new string(' ', 65536) + json)
            : new StringContent(new string(' ', 65536) + json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/configuration/setup", content);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.False(File.Exists(factory.ConfigurationDirectory.FilePath));
        using var valid = await client.PostAsync("/api/configuration/setup", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, valid.StatusCode);
    }
}
