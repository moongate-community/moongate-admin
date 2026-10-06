using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.TestSupport.Configuration;

public static class ConfigurationHttpFixtures
{
    // Public disposable fixture data, never an operator credential.
    public const string SetupToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    public static AdminApiFactory Unconfigured()
    {
        var factory = new AdminApiFactory();
        factory.Settings.Clear();
        factory.Settings["AdminConfiguration:StoragePath"] = factory.ConfigurationDirectory.FilePath;
        factory.Settings["MOONGATE_ADMIN_SETUP_TOKEN"] = SetupToken;
        return factory;
    }
    public static MoongateOptions Candidate(string address, string id = "login")
    {
        return new MoongateOptions { AuthenticationEndpointId = id, AllowInsecureLoopback = true,
            Endpoints = [new MoongateEndpointOptions { Id = id, Label = id, Address = address }] };
    }
}
