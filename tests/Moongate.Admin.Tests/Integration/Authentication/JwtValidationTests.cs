using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moongate.Admin.Api.Services.Authentication;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Authentication;

public class JwtValidationTests
{
    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("session")]
    [InlineData("unsigned")]
    [InlineData("expired")]
    public async Task Read_InvalidJwtContract_Rejects(string scenario)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        var original = new JwtSecurityTokenHandler().ReadJwtToken(client.DefaultRequestHeaders.Authorization?.Parameter);
        var parameters = factory.Services.GetRequiredService<JwtSessionService>().CreateValidationParameters();
        var id = scenario == "session"
            ? "missing-session"
            : original.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Jti).Value;
        var jwt = new JwtSecurityToken(
            scenario == "issuer" ? "wrong-issuer" : "moongate-admin",
            scenario == "audience" ? "wrong-audience" : "moongate-admin-api",
            [new Claim("sub", "7"), new Claim("jti", id), new Claim("role", "administrator")],
            DateTime.UtcNow.AddMinutes(-10),
            scenario == "expired" ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(5),
            scenario == "unsigned" ? null : new SigningCredentials(parameters.IssuerSigningKey, SecurityAlgorithms.HmacSha256)
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtSecurityTokenHandler().WriteToken(jwt)
        );
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task Read_TamperedPayload_Rejects()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        var parts = (client.DefaultRequestHeaders.Authorization?.Parameter ?? "").Split('.');
        parts[1] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"sub\":\"7\",\"role\":\"administrator\"}"))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", string.Join('.', parts));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
    }
}
