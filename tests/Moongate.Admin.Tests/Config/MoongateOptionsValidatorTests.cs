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
    [Theory]
    [InlineData(16, true)]
    [InlineData(17, false)]
    public void Validate_EndpointCount_EnforcesLimit(int count, bool succeeds)
    {
        var options = CreateOptions("https://localhost:2590");
        options.Endpoints = Enumerable.Range(0, count).Select(index =>
            new MoongateEndpointOptions { Id = "server-" + index, Label = "Server", Address = "https://localhost:2590" }).ToArray();
        options.AuthenticationEndpointId = "server-0";
        Assert.Equal(succeeds, CreateValidator("Development").Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Validate_IdLength_EnforcesLimit(int length, bool succeeds)
    {
        var options = CreateOptions("https://localhost:2590");
        options.Endpoints[0].Id = new string('a', length);
        options.AuthenticationEndpointId = options.Endpoints[0].Id;
        Assert.Equal(succeeds, CreateValidator("Development").Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("server id")]
    [InlineData("srv/one")]
    [InlineData("sérver")]
    public void Validate_InvalidIdCharacters_Fails(string id)
    {
        var options = CreateOptions("https://localhost:2590");
        options.Endpoints[0].Id = id;
        options.AuthenticationEndpointId = id;
        Assert.True(CreateValidator("Development").Validate(null, options).Failed);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Validate_LabelLength_EnforcesLimit(int length, bool succeeds)
    {
        var options = CreateOptions("https://localhost:2590");
        options.Endpoints[0].Label = new string('a', length);
        Assert.Equal(succeeds, CreateValidator("Development").Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("Server\nName")]
    [InlineData("Server\0Name")]
    public void Validate_LabelControls_Fails(string label)
    {
        var options = CreateOptions("https://localhost:2590");
        options.Endpoints[0].Label = label;
        Assert.True(CreateValidator("Development").Validate(null, options).Failed);
    }

    [Fact]
    public void Validate_NullCollectionOrEntry_Fails()
    {
        var options = CreateOptions("https://localhost:2590");
        // Deliberately malformed data from a persisted document or HTTP input.
        options.Endpoints = null!;
        Assert.True(CreateValidator("Development").Validate(null, options).Failed);
        options.Endpoints = [null!];
        Assert.True(CreateValidator("Development").Validate(null, options).Failed);
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
