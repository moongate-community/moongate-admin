using System.Net.Http.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;
using Serilog.Events;

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
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.GetProperty("accessToken").GetString());
        foreach (var route in new[] { "/api/auth/session", "/api/servers", "/api/servers/login", "/api/accounts" })
        {
            var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain(marker, await response.Content.ReadAsStringAsync());
            Assert.DoesNotContain(grpc.Authority.Token, await response.Content.ReadAsStringAsync());
        }

        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/accounts", new { username = "New", password = marker })).StatusCode
        );
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/accounts/8/revoke-sessions", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/accounts?pageSize=" + marker)).StatusCode);
        grpc.Authority.LoseCreateResponse = true;
        Assert.Equal(HttpStatusCode.GatewayTimeout, (await client.PostAsJsonAsync("/api/accounts", new { username = "Unknown", password = marker })).StatusCode);
        grpc.Authority.LoseCreateResponse = false;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
        var audit = factory.Logs.Events.ToArray();
        Assert.Contains(audit, entry => entry.MessageTemplate.Text.Contains("Administration operation"));
        foreach (var operation in new[] { "/api/auth/login", "/api/servers", "/api/accounts", "/api/accounts/{id}/revoke-sessions", "/api/auth/logout" })
        {
            Assert.Contains(audit, entry => entry.Properties.TryGetValue("Operation", out var value) && value is ScalarValue { Value: string route } && route == operation);
        }

        Assert.Contains(audit, entry => entry.Properties.TryGetValue("Operation", out var operation) && operation is ScalarValue { Value: "/api/accounts" } && entry.Properties.TryGetValue("Status", out var status) && status is ScalarValue { Value: 504 });
        var logged = string.Join("\n", audit.Select(entry => entry.RenderMessage() + entry.Exception?.ToString()));
        Assert.DoesNotContain(marker, logged);
        Assert.DoesNotContain(grpc.Authority.Token, logged);
    }

    [Fact]
    public async Task Api_PlainHttp_RejectsBearerOperations()
    {
        await using var factory = new AdminApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }
}
