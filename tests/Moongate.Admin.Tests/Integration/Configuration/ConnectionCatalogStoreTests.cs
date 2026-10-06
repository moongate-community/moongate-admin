using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moongate.Admin.Api.Data.Internal.Configuration;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Config;
using Moongate.Admin.Tests.TestSupport.Configuration;

namespace Moongate.Admin.Tests.Integration.Configuration;

public class ConnectionCatalogStoreTests
{
    [Fact]
    public async Task Setup_InitialCatalog_PersistsAndSurvivesRestart()
    {
        using var directory = new TemporaryConfigurationDirectory();
        using var store = ConfigurationFixtures.Store(directory);
        await store.StartAsync(CancellationToken.None);
        Assert.False(store.Current.Configured);
        var saved = await store.SetupAsync(ConfigurationFixtures.Catalog(), CancellationToken.None);
        Assert.True(saved.Configured);
        Assert.True(File.Exists(directory.FilePath));
        using var restarted = ConfigurationFixtures.Store(directory);
        await restarted.StartAsync(CancellationToken.None);
        Assert.Equal(saved.Revision, restarted.Current.Revision);
        Assert.Equal("login", restarted.Current.AuthenticationEndpointId);
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => restarted.SetupAsync(ConfigurationFixtures.Catalog(), CancellationToken.None));
        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public async Task Start_SavedCatalog_WinsOverInvalidStaticConfiguration()
    {
        using var directory = new TemporaryConfigurationDirectory();
        using var original = ConfigurationFixtures.Store(directory);
        await original.StartAsync(CancellationToken.None);
        await original.SetupAsync(ConfigurationFixtures.Catalog("saved"), CancellationToken.None);
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Moongate:AuthenticationEndpointId"] = "missing",
            ["Moongate:Endpoints:0:Id"] = "invalid",
            ["Moongate:Endpoints:0:Address"] = "bad"
        }).Build();
        using var restarted = ConfigurationFixtures.Store(directory, settings);
        await restarted.StartAsync(CancellationToken.None);
        Assert.Equal("saved", restarted.Current.AuthenticationEndpointId);
        Assert.Single(restarted.Current.Endpoints);
    }

    [Fact]
    public async Task Start_PartialStaticCatalog_RejectsStartup()
    {
        using var directory = new TemporaryConfigurationDirectory();
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Moongate:AuthenticationEndpointId"] = "missing" }).Build();
        using var store = ConfigurationFixtures.Store(directory, settings);
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => store.StartAsync(CancellationToken.None));
        Assert.Equal("configuration_load_failed", error.Code);
    }

    [Fact]
    public async Task Replace_RemovedEndpointAndMutableInput_DoesNotRetainOldOrSharedState()
    {
        using var directory = new TemporaryConfigurationDirectory();
        using var store = ConfigurationFixtures.Store(directory);
        await store.StartAsync(CancellationToken.None);
        var old = await store.SetupAsync(ConfigurationFixtures.Catalog(), CancellationToken.None);
        var input = ConfigurationFixtures.Catalog("replacement");
        var replacement = await store.ReplaceAsync(input, old.Revision, CancellationToken.None);
        input.Endpoints[0].Address = "https://mutated.invalid";
        replacement.ToOptions().Endpoints[0].Address = "https://other.invalid";
        Assert.Equal("https://localhost:2590", store.Current.Endpoints.Single().Address);
        Assert.Equal("replacement", store.Current.Endpoints.Single().Id);
        var stale = await Assert.ThrowsAsync<ConfigurationException>(() => store.ReplaceAsync(input, old.Revision, CancellationToken.None));
        Assert.Equal(412, stale.StatusCode);
        Assert.Equal(replacement.Revision, store.Current.Revision);
    }

    [Fact]
    public async Task Setup_ConcurrentRequests_HasOneWinner()
    {
        using var directory = new TemporaryConfigurationDirectory();
        using var store = ConfigurationFixtures.Store(directory);
        await store.StartAsync(CancellationToken.None);
        var writes = new[] { store.SetupAsync(ConfigurationFixtures.Catalog("a"), CancellationToken.None),
            store.SetupAsync(ConfigurationFixtures.Catalog("b"), CancellationToken.None) };
        try { await Task.WhenAll(writes); } catch (ConfigurationException) { }
        Assert.Single(writes, task => task.IsCompletedSuccessfully);
        var failure = Assert.Single(writes, task => task.IsFaulted);
        Assert.Equal(409, Assert.IsType<ConfigurationException>(failure.Exception?.InnerException).StatusCode);
        using var restarted = ConfigurationFixtures.Store(directory);
        await restarted.StartAsync(CancellationToken.None);
        Assert.Equal(store.Current.Revision, restarted.Current.Revision);
    }

    [Fact]
    public async Task Replace_ConcurrentSameRevision_HasOneWinner()
    {
        using var directory = new TemporaryConfigurationDirectory();
        using var store = ConfigurationFixtures.Store(directory);
        await store.StartAsync(CancellationToken.None);
        var initial = await store.SetupAsync(ConfigurationFixtures.Catalog(), CancellationToken.None);
        var writes = new[] { store.ReplaceAsync(ConfigurationFixtures.Catalog("a"), initial.Revision, CancellationToken.None),
            store.ReplaceAsync(ConfigurationFixtures.Catalog("b"), initial.Revision, CancellationToken.None) };
        try { await Task.WhenAll(writes); } catch (ConfigurationException) { }
        Assert.Single(writes, task => task.IsCompletedSuccessfully);
        Assert.Equal(412, Assert.IsType<ConfigurationException>(Assert.Single(writes, task => task.IsFaulted).Exception?.InnerException).StatusCode);
    }

    [Fact]
    public async Task Replace_CancelledAfterCommit_PublishesSavedRevision()
    {
        using var directory = new TemporaryConfigurationDirectory();
        using var cancellation = new CancellationTokenSource();
        var persistence = new ControlledCatalogPersistence(new FileConnectionCatalogPersistence(directory.FilePath));
        var validator = new MoongateOptionsValidator(Host.CreateApplicationBuilder().Environment);
        using var store = new ConnectionCatalogStore(new ConfigurationBuilder().Build(), persistence, validator);
        await store.StartAsync(CancellationToken.None);
        var initial = await store.SetupAsync(ConfigurationFixtures.Catalog(), CancellationToken.None);
        persistence.AfterCommit = cancellation.Cancel;
        var saved = await store.ReplaceAsync(ConfigurationFixtures.Catalog("new"), initial.Revision, cancellation.Token);
        Assert.True(cancellation.IsCancellationRequested);
        using var restarted = ConfigurationFixtures.Store(directory);
        await restarted.StartAsync(CancellationToken.None);
        Assert.Equal(saved.Revision, restarted.Current.Revision);
        Assert.Equal(saved.Revision, store.Current.Revision);
    }

    [Fact]
    public async Task Replace_WriteFailureOrPreCommitCancellation_PreservesState()
    {
        using var directory = new TemporaryConfigurationDirectory();
        var persistence = new ControlledCatalogPersistence(new FileConnectionCatalogPersistence(directory.FilePath));
        using var store = new ConnectionCatalogStore(new ConfigurationBuilder().Build(), persistence,
            new MoongateOptionsValidator(Host.CreateApplicationBuilder().Environment));
        await store.StartAsync(CancellationToken.None);
        var initial = await store.SetupAsync(ConfigurationFixtures.Catalog(), CancellationToken.None);
        var bytes = await File.ReadAllBytesAsync(directory.FilePath);
        persistence.FailWrite = true;
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => store.ReplaceAsync(ConfigurationFixtures.Catalog("new"), initial.Revision, CancellationToken.None));
        Assert.Equal("configuration_save_failed", error.Code);
        persistence.FailWrite = false;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReplaceAsync(ConfigurationFixtures.Catalog("new"), initial.Revision, cancelled.Token));
        Assert.Equal(initial.Revision, store.Current.Revision);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(directory.FilePath));
    }
}
