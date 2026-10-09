using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Contract;

public class OpenApiContractTests
{
    [Fact]
    public async Task OpenApi_Development_DescribesPublicRestContract()
    {
        await using var factory = new AdminApiFactory();
        var response = await AuthenticatedApiClient.Create(factory).GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        var paths = document.GetProperty("paths");
        foreach (var path in new[]
                 {
                     "/health/live", "/api/auth/login", "/api/auth/logout", "/api/auth/session", "/api/servers",
                     "/api/servers/{id}", "/api/accounts", "/api/accounts/{id}/revoke-sessions"
                 })
        {
            Assert.True(paths.TryGetProperty(path, out _), path);
        }

        Assert.True(paths.GetProperty("/api/accounts").GetProperty("post").GetProperty("responses").TryGetProperty("201", out _));
        Assert.True(
            paths.GetProperty("/api/accounts/{id}/revoke-sessions")
                .GetProperty("post")
                .GetProperty("responses")
                .TryGetProperty("204", out _)
        );
        var security = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("AdminBearer");
        Assert.Equal("http", security.GetProperty("type").GetString());
        Assert.Equal("bearer", security.GetProperty("scheme").GetString());
        var schemas = document.GetProperty("components").GetProperty("schemas");
        Assert.Equal(
            "string",
            schemas.GetProperty("ServerInfoResponse").GetProperty("properties").GetProperty("uptimeSeconds").GetProperty("type").GetString()
        );
        var role = schemas.GetProperty("AccountSummaryResponse").GetProperty("properties").GetProperty("accountType");
        if (role.TryGetProperty("$ref", out var reference))
        {
            role = schemas.GetProperty(reference.GetString()!.Split('/').Last());
        }

        Assert.Equal("string", role.GetProperty("type").GetString());
        Assert.Contains(role.GetProperty("enum").EnumerateArray(), value => value.GetString() == "administrator");
        var login = schemas.GetProperty("LoginRequest");
        Assert.True(login.GetProperty("properties").GetProperty("password").GetProperty("writeOnly").GetBoolean());
        Assert.Contains(login.GetProperty("required").EnumerateArray(), field => field.GetString() == "username");
        Assert.True(schemas.GetProperty("JwtLoginResponse").GetProperty("properties").TryGetProperty("accessToken", out _));
        Assert.DoesNotContain("UpstreamLoginResult", document.ToString());
        var loginSecurity = paths.GetProperty("/api/auth/login").GetProperty("post").GetProperty("security");
        Assert.Contains(loginSecurity.EnumerateArray(), requirement => !requirement.EnumerateObject().Any());
    }

    [Fact]
    public async Task OpenApi_Production_ReturnsNotFound()
    {
        await using var factory = new AdminApiFactory { EnvironmentName = "Production" };
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await AuthenticatedApiClient.Create(factory).GetAsync("/openapi/v1.json")).StatusCode
        );
    }
}
