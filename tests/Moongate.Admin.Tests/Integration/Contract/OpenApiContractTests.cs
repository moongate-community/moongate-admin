using System.Net.Http.Json;
using System.Net;
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
        Assert.True(paths.TryGetProperty("/health/live", out _));
        foreach (var path in new[]
                 {
                     "/api/auth/login", "/api/auth/logout", "/api/auth/session", "/api/servers",
                     "/api/servers/{id}", "/api/accounts", "/api/accounts/{id}/revoke-sessions"
                 })
        {
            Assert.True(paths.TryGetProperty(path, out _), path);
        }

        var create = paths.GetProperty("/api/accounts").GetProperty("post");
        Assert.True(create.GetProperty("responses").TryGetProperty("201", out _));
        var revoke = paths.GetProperty("/api/accounts/{id}/revoke-sessions").GetProperty("post");
        Assert.True(revoke.GetProperty("responses").TryGetProperty("204", out _));
        var security = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("AdminBearer");
        Assert.Equal("http", security.GetProperty("type").GetString());
        Assert.Equal("bearer", security.GetProperty("scheme").GetString());
        Assert.False(paths.TryGetProperty("/api/auth/csrf", out _));
        var schemas = document.GetProperty("components").GetProperty("schemas");
        Assert.Equal(
            "integer",
            schemas.GetProperty("AccountSummaryResponse")
                .GetProperty("properties")
                .GetProperty("accountId")
                .GetProperty("type")
                .GetString()
        );
        Assert.Equal(
            "string",
            schemas.GetProperty("ServerInfoResponse")
                .GetProperty("properties")
                .GetProperty("uptimeSeconds")
                .GetProperty("type")
                .GetString()
        );
        var roleSchema = schemas.GetProperty("AccountSummaryResponse").GetProperty("properties").GetProperty("accountType");
        if (roleSchema.TryGetProperty("$ref", out var roleReference))
        {
            var name = roleReference.GetString()?.Split('/').Last() ?? throw new InvalidOperationException("Missing role schema reference.");
            roleSchema = schemas.GetProperty(name);
        }
        Assert.Equal("string", roleSchema.GetProperty("type").GetString());
        Assert.Contains(roleSchema.GetProperty("enum").EnumerateArray(), value => value.GetString() == "administrator");
        var loginSchema = schemas.GetProperty("LoginRequest");
        Assert.Equal(
            "string",
            loginSchema.GetProperty("properties").GetProperty("username").GetProperty("type").GetString()
        );
        Assert.True(loginSchema.GetProperty("properties").GetProperty("password").GetProperty("writeOnly").GetBoolean());
        Assert.Contains(loginSchema.GetProperty("required").EnumerateArray(), field => field.GetString() == "username");
        Assert.Contains(loginSchema.GetProperty("required").EnumerateArray(), field => field.GetString() == "password");
        Assert.True(schemas.GetProperty("JwtLoginResponse").GetProperty("properties").TryGetProperty("accessToken", out _));
        Assert.DoesNotContain("UpstreamLoginResult", document.ToString());
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
