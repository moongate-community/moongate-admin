using Microsoft.Extensions.Hosting;
using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Services.Config;

namespace Moongate.Admin.Tests.Config;

public class MoongateOptionsValidatorTests
{
    [Theory]
    [InlineData("https://user:pass@host:2590")]
    [InlineData("https://host:2590/?query=true")]
    [InlineData("https://host:2590/#fragment")]
    [InlineData("ftp://host:2590")]
    [InlineData("http://remote:2590")]
    public void Validate_UnsafeAddress_Fails(string address)
    {
        var options = CreateOptions(address);
        Assert.True(CreateValidator("Development").Validate(null, options).Failed);
    }
    [Fact]
    public void Validate_MissingAuthenticationEndpoint_Fails()
    {
        var options = CreateOptions("https://127.0.0.1:2590");
        options.AuthenticationEndpointId = "missing";
        Assert.True(CreateValidator("Development").Validate(null, options).Failed);
    }
    [Fact]
    public void Validate_DuplicateEndpointId_Fails()
    {
        var options = CreateOptions("https://127.0.0.1:2590");
        options.Endpoints = [options.Endpoints[0], new MoongateEndpointOptions { Id = "login", Label = "Other", Address = "https://127.0.0.1:2591" }];
        Assert.True(CreateValidator("Development").Validate(null, options).Failed);
    }
    [Theory]
    [InlineData("Development", true, true)]
    [InlineData("Development", false, false)]
    [InlineData("Production", true, false)]
    public void Validate_LoopbackHttp_RequiresExplicitDevelopment(string environment, bool allow, bool succeeds)
    {
        var options = CreateOptions("http://127.0.0.1:2590");
        options.AllowInsecureLoopback = allow;
        Assert.Equal(succeeds, CreateValidator(environment).Validate(null, options).Succeeded);
    }
    private static MoongateOptions CreateOptions(string address)
    {
        return new MoongateOptions
        {
            AuthenticationEndpointId = "login",
            Endpoints = [new MoongateEndpointOptions { Id = "login", Label = "Login", Address = address }]
        };
    }
    private static MoongateOptionsValidator CreateValidator(string environment)
    {
        return new MoongateOptionsValidator(Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment }).Environment);
    }
}
