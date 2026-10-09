using Microsoft.Extensions.Hosting;
using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Services.Config;

namespace Moongate.Admin.Tests.Config;

public class MoongateOptionsValidatorTests
{
    [Fact]
    public void Validate_EmptySection_Succeeds()
    {
        Assert.True(CreateValidator("Production").Validate(null, new MoongateOptions()).Succeeded);
    }

    [Theory]
    [InlineData("https://user:pass@host:2590")]
    [InlineData("https://host:2590/?query=true")]
    [InlineData("https://host:2590/#fragment")]
    [InlineData("https://host:2590/path")]
    [InlineData("ftp://host:2590")]
    [InlineData("http://remote:2590")]
    [InlineData("not-a-url")]
    public void Validate_UnsafeAddress_Fails(string address)
    {
        Assert.True(CreateValidator("Development").Validate(null, CreateOptions(address)).Failed);
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
        options.Endpoints =
        [
            options.Endpoints[0],
            new MoongateEndpointOptions { Id = "login", Label = "Other", Address = "https://127.0.0.1:2591" }
        ];
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
    [InlineData("http://localhost:2590")]
    [InlineData("http://192.0.2.10:2590")]
    public void Validate_NonLiteralLoopbackHttp_Fails(string address)
    {
        var options = CreateOptions(address);
        options.AllowInsecureLoopback = true;
        Assert.True(CreateValidator("Development").Validate(null, options).Failed);
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(17, false)]
    public void Validate_EndpointCount_EnforcesLimit(int count, bool succeeds)
    {
        var options = CreateOptions("https://localhost:2590");
        options.Endpoints = Enumerable.Range(0, count)
            .Select(index =>
                new MoongateEndpointOptions { Id = "server-" + index, Label = "Server", Address = "https://localhost:2590" }
            )
            .ToArray();
        options.AuthenticationEndpointId = "server-0";
        Assert.Equal(succeeds, CreateValidator("Development").Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Validate_IdLength_EnforcesLimit(int length, bool succeeds)
    {
        var options = CreateOptions("https://localhost:2590");
        var id = new string('a', length);
        options.Endpoints = [new MoongateEndpointOptions { Id = id, Label = "L", Address = "https://localhost:2590" }];
        options.AuthenticationEndpointId = id;
        Assert.Equal(succeeds, CreateValidator("Development").Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("server id")]
    [InlineData("srv/one")]
    [InlineData("sérver")]
    public void Validate_InvalidIdCharacters_Fails(string id)
    {
        var options = CreateOptions("https://localhost:2590");
        options.Endpoints = [new MoongateEndpointOptions { Id = id, Label = "L", Address = "https://localhost:2590" }];
        options.AuthenticationEndpointId = id;
        Assert.True(CreateValidator("Development").Validate(null, options).Failed);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Validate_LabelLength_EnforcesLimit(int length, bool succeeds)
    {
        var options = CreateOptions("https://localhost:2590");
        options.Endpoints =
        [
            new MoongateEndpointOptions { Id = "login", Label = new string('a', length), Address = "https://localhost:2590" }
        ];
        Assert.Equal(succeeds, CreateValidator("Development").Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("Server\nName")]
    [InlineData("Server\0Name")]
    [InlineData("   ")]
    public void Validate_BadLabel_Fails(string label)
    {
        var options = CreateOptions("https://localhost:2590");
        options.Endpoints = [new MoongateEndpointOptions { Id = "login", Label = label, Address = "https://localhost:2590" }];
        Assert.True(CreateValidator("Development").Validate(null, options).Failed);
    }

    [Fact]
    public void Validate_NullEntry_Fails()
    {
        var options = CreateOptions("https://localhost:2590");
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
        return new MoongateOptionsValidator(
            Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment }).Environment
        );
    }
}
