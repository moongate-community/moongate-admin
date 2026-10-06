using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Configuration;

namespace Moongate.Admin.Tests.Integration.Configuration;

public class ConfigurationSetupEndpointTests
{
    [Fact]
    public async Task Setup_ValidToken_PersistsAndClosesBootstrapAfterRestart()
    {
        await using var factory = ConfigurationHttpFixtures.Unconfigured();
        using var client = AuthenticatedApiClient.Create(factory);
        var before = await client.GetFromJsonAsync<JsonElement>("/api/configuration/status");
        Assert.False(before.GetProperty("configured").GetBoolean());
        Assert.True(before.GetProperty("setupAvailable").GetBoolean());
        Assert.Equal(2, before.EnumerateObject().Count());
        client.DefaultRequestHeaders.Add("X-Moongate-Setup-Token", ConfigurationHttpFixtures.SetupToken);
        var saved = await client.PostAsJsonAsync(
            "/api/configuration/setup",
            ConfigurationHttpFixtures.Candidate("https://localhost:2590")
        );
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        Assert.NotNull(saved.Headers.ETag);
        var after = await client.GetFromJsonAsync<JsonElement>("/api/configuration/status");
        Assert.True(after.GetProperty("configured").GetBoolean());
        Assert.False(after.GetProperty("setupAvailable").GetBoolean());
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(
                "/api/configuration/setup",
                ConfigurationHttpFixtures.Candidate("https://other:2590")
            )).StatusCode
        );
        var contents = await File.ReadAllTextAsync(factory.ConfigurationDirectory.FilePath);
        Assert.DoesNotContain(ConfigurationHttpFixtures.SetupToken, contents);
        await using var restarted = ConfigurationHttpFixtures.Unconfigured();
        restarted.Settings["AdminConfiguration:StoragePath"] = factory.ConfigurationDirectory.FilePath;
        using var restartedClient = AuthenticatedApiClient.Create(restarted);
        Assert.False(
            (await restartedClient.GetFromJsonAsync<JsonElement>("/api/configuration/status")).GetProperty("setupAvailable")
            .GetBoolean()
        );
        Assert.DoesNotContain(
            ConfigurationHttpFixtures.SetupToken,
            string.Join("\n", factory.Logs.Events.Select(item => item.RenderMessage()))
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("incorrect")]
    public async Task Setup_MissingOrWrongToken_IsUnauthorized(string? token)
    {
        await using var factory = ConfigurationHttpFixtures.Unconfigured();
        using var client = AuthenticatedApiClient.Create(factory);
        if (token is not null)
        {
            client.DefaultRequestHeaders.Add("X-Moongate-Setup-Token", token);
        }

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync(
                "/api/configuration/setup",
                ConfigurationHttpFixtures.Candidate("https://localhost:2590")
            )).StatusCode
        );
        Assert.False(File.Exists(factory.ConfigurationDirectory.FilePath));
    }

    [Fact]
    public async Task Setup_ConcurrentHttpRequests_OnlyOneCommits()
    {
        await using var factory = ConfigurationHttpFixtures.Unconfigured();
        using var client = AuthenticatedApiClient.Create(factory);
        client.DefaultRequestHeaders.Add("X-Moongate-Setup-Token", ConfigurationHttpFixtures.SetupToken);
        var results = await Task.WhenAll(
            client.PostAsJsonAsync(
                "/api/configuration/setup",
                ConfigurationHttpFixtures.Candidate("https://localhost:2590", "a")
            ),
            client.PostAsJsonAsync(
                "/api/configuration/setup",
                ConfigurationHttpFixtures.Candidate("https://localhost:2591", "b")
            )
        );
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Status_MissingRuntimeToken_DoesNotOfferSetup()
    {
        await using var factory = ConfigurationHttpFixtures.Unconfigured();
        factory.Settings.Remove("MOONGATE_ADMIN_SETUP_TOKEN");
        using var client = AuthenticatedApiClient.Create(factory);
        Assert.False(
            (await client.GetFromJsonAsync<JsonElement>("/api/configuration/status")).GetProperty("setupAvailable")
            .GetBoolean()
        );
    }

    [Fact]
    public async Task Setup_ExistingStaticCatalog_CannotBeTakenOver()
    {
        await using var factory = new TestSupport.Hosting.AdminApiFactory();
        factory.Settings["MOONGATE_ADMIN_SETUP_TOKEN"] = ConfigurationHttpFixtures.SetupToken;
        using var client = AuthenticatedApiClient.Create(factory);
        client.DefaultRequestHeaders.Add("X-Moongate-Setup-Token", ConfigurationHttpFixtures.SetupToken);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(
                "/api/configuration/setup",
                ConfigurationHttpFixtures.Candidate("https://other:2590")
            )).StatusCode
        );
    }
}
