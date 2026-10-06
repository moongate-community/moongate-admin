using Microsoft.Extensions.Configuration;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Config;
using Moongate.Admin.Tests.TestSupport.Configuration;

namespace Moongate.Admin.Tests.Config;

public class SetupTokenServiceTests
{
    [Fact]
    public async Task Start_ValidRuntimeToken_VerifiesOnlyExactHeader()
    {
        using var service = new SetupTokenService(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["MOONGATE_ADMIN_SETUP_TOKEN"] = ConfigurationHttpFixtures.SetupToken }).Build());
        await service.StartAsync(CancellationToken.None);
        Assert.True(service.Available);
        Assert.True(service.Verify(ConfigurationHttpFixtures.SetupToken));
        Assert.False(service.Verify(null));
        Assert.False(service.Verify("incorrect"));
    }
    [Theory]
    [InlineData("fixture-malformed-runtime-token")]
    [InlineData("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    public async Task Start_InvalidConfiguredToken_FailsWithoutEchoingIt(string value)
    {
        using var service = new SetupTokenService(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["MOONGATE_ADMIN_SETUP_TOKEN"] = value }).Build());
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => service.StartAsync(CancellationToken.None));
        Assert.DoesNotContain(value, error.Message);
    }
    [Fact]
    public async Task Start_AbsentToken_IsUnavailable()
    {
        using var service = new SetupTokenService(new ConfigurationBuilder().Build());
        await service.StartAsync(CancellationToken.None);
        Assert.False(service.Available);
        Assert.False(service.Verify(ConfigurationHttpFixtures.SetupToken));
    }
}
