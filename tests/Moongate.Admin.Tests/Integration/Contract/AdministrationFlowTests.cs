using System.Net.Http.Json;
using System.Net;
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
        await AuthenticatedApiClient.RefreshCsrfAsync(client);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = marker });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await AuthenticatedApiClient.RefreshCsrfAsync(client);
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
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
        var audit = factory.Logs.Events.ToArray();
        Assert.Contains(audit, entry => entry.MessageTemplate.Text.Contains("Administration operation"));
        var logged = string.Join("\n", audit.Select(entry => entry.RenderMessage() + entry.Exception?.ToString()));
        Assert.DoesNotContain(marker, logged);
        Assert.DoesNotContain(grpc.Authority.Token, logged);
    }

    [Fact]
    public async Task Api_PlainHttp_RejectsBeforeIssuingCsrf()
    {
        await using var factory = new AdminApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }
}
