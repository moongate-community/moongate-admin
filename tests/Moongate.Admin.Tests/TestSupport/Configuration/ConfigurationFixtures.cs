using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Services.Config;

namespace Moongate.Admin.Tests.TestSupport.Configuration;

public static class ConfigurationFixtures
{
    public static MoongateOptions Catalog(string id = "login", string address = "https://localhost:2590")
    {
        return new MoongateOptions
        {
            AuthenticationEndpointId = id,
            Endpoints = [new MoongateEndpointOptions { Id = id, Label = id, Address = address }]
        };
    }
    public static ConnectionCatalogStore Store(TemporaryConfigurationDirectory directory, IConfiguration? configuration = null)
    {
        var environment = Host.CreateApplicationBuilder().Environment;
        return new ConnectionCatalogStore(configuration ?? new ConfigurationBuilder().Build(),
            new FileConnectionCatalogPersistence(directory.FilePath), new MoongateOptionsValidator(environment));
    }
}
