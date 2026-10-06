using Moongate.Admin.Api.Data.Internal.Configuration;
using Moongate.Admin.Api.Services.Config;
using Moongate.Admin.Tests.TestSupport.Configuration;

namespace Moongate.Admin.Tests.Integration.Configuration;

public class FileConnectionCatalogPersistenceTests
{
    [Fact]
    public async Task Write_ReadRoundTrip_ContainsOnlyCatalogAndSchema()
    {
        using var directory = new TemporaryConfigurationDirectory();
        var file = new FileConnectionCatalogPersistence(directory.FilePath);
        Assert.Null(await file.ReadAsync(CancellationToken.None));
        await file.WriteAsync(new PersistedConnectionCatalog { SchemaVersion = 1, Revision = "11111111111111111111111111111111", Configuration = ConfigurationFixtures.Catalog() }, CancellationToken.None);
        var saved = await file.ReadAsync(CancellationToken.None);
        Assert.Equal("11111111111111111111111111111111", saved?.Revision);
        var text = await File.ReadAllTextAsync(directory.FilePath);
        Assert.Contains("\"schemaVersion\": 1", text);
        Assert.DoesNotContain("password", text);
        Assert.Empty(Directory.GetFiles(directory.Root, "*.tmp"));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":2,\"revision\":\"11111111111111111111111111111111\",\"configuration\":{\"authenticationEndpointId\":\"login\",\"endpoints\":[]}}")]
    [InlineData("{\"schemaVersion\":1,\"revision\":\"not-a-revision\",\"configuration\":{\"authenticationEndpointId\":\"login\",\"endpoints\":[]}}")]
    public async Task Start_CorruptSavedDocument_DoesNotFallBack(string contents)
    {
        using var directory = new TemporaryConfigurationDirectory();
        await File.WriteAllTextAsync(directory.FilePath, contents);
        using var store = ConfigurationFixtures.Store(directory);
        var error = await Assert.ThrowsAsync<Api.Internal.ConfigurationException>(() => store.StartAsync(CancellationToken.None));
        Assert.Equal("configuration_load_failed", error.Code);
    }

    [Fact]
    public async Task Write_RenameFailure_CleansTemporaryFile()
    {
        using var directory = new TemporaryConfigurationDirectory();
        Directory.CreateDirectory(directory.FilePath);
        var file = new FileConnectionCatalogPersistence(directory.FilePath);
        await Assert.ThrowsAnyAsync<IOException>(() => file.WriteAsync(
            new PersistedConnectionCatalog { SchemaVersion = 1, Revision = "11111111111111111111111111111111", Configuration = ConfigurationFixtures.Catalog() }, CancellationToken.None));
        Assert.Empty(Directory.GetFiles(directory.Root, "*.tmp"));
    }
}
