using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Interfaces.Configuration;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Configuration;

public class ConfigurationRevisionTests
{
    [Fact]
    public async Task Login_EmptyDefaultCatalog_RequiresConfiguration()
    {
        await using var factory = new AdminApiFactory();
        factory.Settings.Clear();
        factory.Settings["AdminConfiguration:StoragePath"] = factory.ConfigurationDirectory.FilePath;
        using var client = AuthenticatedApiClient.Create(factory);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, login.StatusCode);
        Assert.Equal(
            "configuration_required",
            (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
        );
    }

    [Fact]
    public async Task Replace_OldJwt_RejectsWithoutContactingReplacementAuthority()
    {
        await using var first = await AdminGrpcFixture.StartAsync(instanceId: "first");
        await using var second = await AdminGrpcFixture.StartAsync(instanceId: "second");
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(first);
        using var oldClient = await AuthenticatedApiClient.CreateAsync(factory);
        var store = factory.Services.GetRequiredService<IConnectionCatalogStore>();
        await store.ReplaceAsync(Candidate(second), store.Current.Revision, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldClient.GetAsync("/api/auth/session")).StatusCode);
        Assert.Equal(0, second.Authority.LoginCallCount);
        Assert.Null(second.Authority.LastAuthorization);
        using var newClient = await AuthenticatedApiClient.CreateAsync(factory);
        var info = await newClient.GetFromJsonAsync<JsonElement>("/api/servers/login");
        Assert.Equal("second", info.GetProperty("instanceId").GetString());
        Assert.Equal(1, second.Authority.LoginCallCount);
    }

    [Fact]
    public async Task Replace_AfterJwtValidation_KeepsRequestOnOriginalAuthority()
    {
        await using var first = await AdminGrpcFixture.StartAsync(instanceId: "first");
        await using var second = await AdminGrpcFixture.StartAsync(instanceId: "second");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var factory = new AdminApiFactory
        { ValidatedRequestEntered = entered, ValidatedRequestRelease = release };
        factory.UseGrpc(first);
        using var client = await AuthenticatedApiClient.CreateAsync(factory);
        var pending = client.GetAsync("/api/servers/login");
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var store = factory.Services.GetRequiredService<IConnectionCatalogStore>();
            await store.ReplaceAsync(Candidate(second), store.Current.Revision, CancellationToken.None);
        }
        finally
        {
            release.TrySetResult();
        }

        var response = await pending;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "first",
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("instanceId").GetString()
        );
        Assert.Null(second.Authority.LastAuthorization);
    }

    [Fact]
    public async Task Login_FinishesAfterReplacement_ReturnedJwtCannotReachNewAuthority()
    {
        var authority = new FakeAdminAuthority
        {
            LoginEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            LoginRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        await using var first = await AdminGrpcFixture.StartAsync(authority);
        await using var second = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(first);
        using var client = AuthenticatedApiClient.Create(factory);
        var loginTask = client.PostAsJsonAsync("/api/auth/login", new { username = "Admin", password = "fixture-only" });
        try
        {
            await authority.LoginEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var store = factory.Services.GetRequiredService<IConnectionCatalogStore>();
            await store.ReplaceAsync(Candidate(second), store.Current.Revision, CancellationToken.None);
        }
        finally
        {
            authority.LoginRelease.TrySetResult();
        }

        var login = await (await loginTask).Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            login.GetProperty("accessToken").GetString()
        );
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
        Assert.Null(second.Authority.LastAuthorization);
    }

    [Fact]
    public async Task Login_SavedCatalogWithInvalidStaticSettings_UsesPersistedAuthority()
    {
        await using var upstream = await AdminGrpcFixture.StartAsync(instanceId: "persisted");
        await using var firstFactory = new AdminApiFactory();
        firstFactory.UseGrpc(upstream);
        using var firstClient = await AuthenticatedApiClient.CreateAsync(firstFactory);
        var store = firstFactory.Services.GetRequiredService<IConnectionCatalogStore>();
        await store.ReplaceAsync(Candidate(upstream), store.Current.Revision, CancellationToken.None);
        await using var restarted = new AdminApiFactory();
        restarted.Settings.Clear();
        restarted.Settings["AdminConfiguration:StoragePath"] = firstFactory.ConfigurationDirectory.FilePath;
        restarted.Settings["Moongate:AuthenticationEndpointId"] = "invalid-static";
        using var client = await AuthenticatedApiClient.CreateAsync(restarted);
        Assert.Equal(
            "persisted",
            (await client.GetFromJsonAsync<JsonElement>("/api/servers/login")).GetProperty("instanceId").GetString()
        );
    }

    [Fact]
    public async Task Replace_DuringDispatchedRpc_CompletesOnOriginalAuthority()
    {
        var authority = new FakeAdminAuthority
        {
            InformationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            InformationRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        await using var first = await AdminGrpcFixture.StartAsync(authority, instanceId: "first");
        await using var second = await AdminGrpcFixture.StartAsync(instanceId: "second");
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(first);
        using var client = await AuthenticatedApiClient.CreateAsync(factory);
        var pending = client.GetAsync("/api/servers/login");
        try
        {
            await authority.InformationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var store = factory.Services.GetRequiredService<IConnectionCatalogStore>();
            await store.ReplaceAsync(Candidate(second), store.Current.Revision, CancellationToken.None);
        }
        finally
        {
            authority.InformationRelease.TrySetResult();
        }

        Assert.Equal(
            "first",
            (await (await pending).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("instanceId").GetString()
        );
        Assert.Null(second.Authority.LastAuthorization);
    }

    private static MoongateOptions Candidate(AdminGrpcFixture fixture)
    {
        return new MoongateOptions
        {
            AuthenticationEndpointId = "login", AllowInsecureLoopback = true,
            Endpoints = [new MoongateEndpointOptions { Id = "login", Label = "Login", Address = fixture.Address }]
        };
    }
}
