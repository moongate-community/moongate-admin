using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Contract;

[Collection("Logging")]
public class AdministrationFlowTests
{
    [Fact]
    public async Task Flow_RealHttpAndGrpc_CompletesWithoutCredentialLogs()
    {
        const string marker = "fixture-sensitive-marker";
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = AuthenticatedApiClient.Create(factory);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = marker });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("no-store", login.Headers.CacheControl?.ToString());
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            loginBody.GetProperty("accessToken").GetString()
        );
        foreach (var route in new[] { "/api/auth/session", "/api/servers", "/api/servers/login", "/api/accounts" })
        {
            var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(marker, text);
            Assert.DoesNotContain(grpc.Authority.Token, text);
        }

        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/accounts", new { username = "New", password = marker })).StatusCode
        );
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/accounts/8/revoke-sessions", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/accounts?pageSize=" + marker)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
        var audit = factory.Logs.Events.ToArray();
        Assert.Contains(audit, entry => entry.MessageTemplate.Text.Contains("Administration operation"));
        var logged = string.Join("\n", audit.Select(entry => entry.RenderMessage() + entry.Exception?.ToString()));
        Assert.DoesNotContain(marker, logged);
        Assert.DoesNotContain(grpc.Authority.Token, logged);
    }

    [Fact]
    public async Task Api_PlainHttp_RejectsBearerOperations()
    {
        await using var factory = new AdminApiFactory();
        var response = await factory.CreateClient().GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "https_required",
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
        );
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Unmatched_Route_ReturnsNotFound()
    {
        await using var factory = new AdminApiFactory();
        Assert.Equal(HttpStatusCode.NotFound, (await AuthenticatedApiClient.Create(factory).GetAsync("/nope")).StatusCode);
    }
}
