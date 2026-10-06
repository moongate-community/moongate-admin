using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Configuration;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Configuration;

public class ConnectionProbeEndpointTests
{
[Fact]
    public async Task Probe_GameReportedAsAuthenticationServer_IsRejected()
    {
        await using var candidate = await AdminGrpcFixture.StartAsync(reportedMode: ServerMode.Game);
        await using var factory = ConfigurationHttpFixtures.Unconfigured();
        using var client = AuthenticatedApiClient.Create(factory);
        client.DefaultRequestHeaders.Add("X-Moongate-Setup-Token", ConfigurationHttpFixtures.SetupToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/configuration/test-connection",
            new { configuration = ConfigurationHttpFixtures.Candidate(candidate.Address), username = "Admin", password = "fixture-only" })).StatusCode);
        Assert.True(candidate.Authority.Revoked);
    }

    [Fact]
    public async Task Probe_LogoutOutage_DoesNotOverwriteSuccessfulResult()
    {
        await using var candidate = await AdminGrpcFixture.StartAsync(new FakeAdminAuthority { LogoutFailure = StatusCode.Unavailable });
        await using var factory = ConfigurationHttpFixtures.Unconfigured();
        using var client = AuthenticatedApiClient.Create(factory);
        client.DefaultRequestHeaders.Add("X-Moongate-Setup-Token", ConfigurationHttpFixtures.SetupToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/configuration/test-connection",
            new { configuration = ConfigurationHttpFixtures.Candidate(candidate.Address), username = "Admin", password = "fixture-only" })).StatusCode);
        Assert.Contains(factory.Logs.Events, item => item.Level == Serilog.Events.LogEventLevel.Warning);
        Assert.DoesNotContain("upstream-private-detail", string.Join("\n", factory.Logs.Events.Select(item => item.RenderMessage())));
    }

    [Theory]
    [InlineData(false, "localhost")]
    [InlineData(true, "127.0.0.1")]
    public async Task Probe_InvalidTls_ReturnsUnavailableWithoutCandidateLogin(bool trusted, string hostname)
    {
        using var certificates = new TestGrpcCertificates();
        await using var candidate = await AdminGrpcFixture.StartAsync(certificate: certificates.Server);
        await using var factory = ConfigurationHttpFixtures.Unconfigured();
        if (trusted) { factory.GrpcHandler = certificates.TrustedHandler; }
        using var client = AuthenticatedApiClient.Create(factory);
        client.DefaultRequestHeaders.Add("X-Moongate-Setup-Token", ConfigurationHttpFixtures.SetupToken);
        var address = new UriBuilder(candidate.Address) { Host = hostname }.Uri.ToString();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/configuration/test-connection",
            new { configuration = ConfigurationHttpFixtures.Candidate(address), username = "Admin", password = "fixture-only" })).StatusCode);
        Assert.Equal(0, candidate.Authority.LoginCallCount);
        Assert.False(File.Exists(factory.ConfigurationDirectory.FilePath));
    }

    [Fact]
    public async Task Probe_CallerCancellation_ReachesCandidateRpc()
    {
        var authority = new FakeAdminAuthority
        {
            LoginEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            LoginRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            LoginCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        await using var candidate = await AdminGrpcFixture.StartAsync(authority);
        await using var factory = ConfigurationHttpFixtures.Unconfigured();
        using var client = AuthenticatedApiClient.Create(factory);
        client.DefaultRequestHeaders.Add("X-Moongate-Setup-Token", ConfigurationHttpFixtures.SetupToken);
        using var cancellation = new CancellationTokenSource();
        var pending = client.PostAsJsonAsync("/api/configuration/test-connection",
            new { configuration = ConfigurationHttpFixtures.Candidate(candidate.Address), username = "Admin", password = "fixture-only" }, cancellation.Token);
        try
        {
            await authority.LoginEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
            await authority.LoginCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { authority.LoginRelease.TrySetResult(); }
        Assert.False(File.Exists(factory.ConfigurationDirectory.FilePath));
    }

    [Theory]
    [InlineData("{\"configuration\":null,\"username\":\"Admin\",\"password\":\"fixture-only\"}")]
    [InlineData("{\"configuration\":{\"authenticationEndpointId\":\"a\",\"endpoints\":[null]},\"username\":\"Admin\",\"password\":\"fixture-only\"}")]
    [InlineData("{\"configuration\":{\"authenticationEndpointId\":\"a\",\"endpoints\":null},\"username\":\"Admin\",\"password\":\"fixture-only\"}")]
    public async Task Probe_NullCatalogStructure_IsSafeBadRequest(string json)
    {
        await using var factory = ConfigurationHttpFixtures.Unconfigured();
        using var client = AuthenticatedApiClient.Create(factory);
        client.DefaultRequestHeaders.Add("X-Moongate-Setup-Token", ConfigurationHttpFixtures.SetupToken);
        var response = await client.PostAsync("/api/configuration/test-connection", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("fixture-only", await response.Content.ReadAsStringAsync());
    }
    [Theory]
    [InlineData(ServerMode.Login)]
    [InlineData(ServerMode.Standalone)]
    public async Task Probe_BootstrapCredentials_TestsWithoutPersisting(ServerMode mode)
    {
        await using var grpc = await AdminGrpcFixture.StartAsync(mode: mode);
        await using var factory = ConfigurationHttpFixtures.Unconfigured();
        using var client = AuthenticatedApiClient.Create(factory);
        client.DefaultRequestHeaders.Add("X-Moongate-Setup-Token", ConfigurationHttpFixtures.SetupToken);
        var response = await client.PostAsJsonAsync("/api/configuration/test-connection",
            new { configuration = ConfigurationHttpFixtures.Candidate(grpc.Address), username = "ProbeAdmin", password = "fixture-probe-password" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("fixture-probe-password", text);
        Assert.DoesNotContain(grpc.Authority.Token, text);
        Assert.DoesNotContain("accessToken", text);
        Assert.True(grpc.Authority.Revoked);
        Assert.False(File.Exists(factory.ConfigurationDirectory.FilePath));
        Assert.DoesNotContain("fixture-probe-password", string.Join("\n", factory.Logs.Events.Select(item => item.RenderMessage())));
    }

    [Fact]
    public async Task Probe_AdministratorJwt_CanReadCandidateGame()
    {
        await using var current = await AdminGrpcFixture.StartAsync();
        await using var candidate = await AdminGrpcFixture.StartAsync();
        await using var game = await AdminGrpcFixture.StartAsync(candidate.Authority, mode: ServerMode.Game, instanceId: "game-candidate");
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(current);
        using var client = await AuthenticatedApiClient.CreateAsync(factory);
        var configuration = ConfigurationHttpFixtures.Candidate(candidate.Address);
        configuration.Endpoints = [configuration.Endpoints[0], new Api.Data.Config.MoongateEndpointOptions { Id = "game", Label = "Game", Address = game.Address }];
        var response = await client.PostAsJsonAsync("/api/configuration/test-connection", new { configuration, username = "Admin", password = "fixture-only", endpointId = "game" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("game-candidate", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("server").GetProperty("instanceId").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task Probe_CandidateAuthenticationFailure_PreservesCallerSession()
    {
        await using var current = await AdminGrpcFixture.StartAsync();
        await using var candidate = await AdminGrpcFixture.StartAsync(new FakeAdminAuthority { Failure = StatusCode.Unauthenticated });
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(current);
        using var client = await AuthenticatedApiClient.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/configuration/test-connection",
            new { configuration = ConfigurationHttpFixtures.Candidate(candidate.Address), username = "Admin", password = "fixture-only" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Theory]
    [InlineData(AccountType.Regular)]
    [InlineData(AccountType.GameMaster)]
    public async Task Probe_CandidateNonAdministrator_IsRejected(AccountType role)
    {
        await using var candidate = await AdminGrpcFixture.StartAsync(new FakeAdminAuthority { Role = role });
        await using var factory = ConfigurationHttpFixtures.Unconfigured();
        using var client = AuthenticatedApiClient.Create(factory);
        client.DefaultRequestHeaders.Add("X-Moongate-Setup-Token", ConfigurationHttpFixtures.SetupToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/configuration/test-connection",
            new { configuration = ConfigurationHttpFixtures.Candidate(candidate.Address), username = "Admin", password = "fixture-only" })).StatusCode);
        Assert.True(candidate.Authority.Revoked);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown")]
    public async Task Probe_UnknownEndpointOrBlankCredentials_DoesNotContactCandidate(string? selected)
    {
        await using var candidate = await AdminGrpcFixture.StartAsync();
        await using var factory = ConfigurationHttpFixtures.Unconfigured();
        using var client = AuthenticatedApiClient.Create(factory);
        client.DefaultRequestHeaders.Add("X-Moongate-Setup-Token", ConfigurationHttpFixtures.SetupToken);
        var response = await client.PostAsJsonAsync("/api/configuration/test-connection",
            new { configuration = ConfigurationHttpFixtures.Candidate(candidate.Address), username = selected is null ? "" : "Admin", password = "fixture-only", endpointId = selected });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, candidate.Authority.LoginCallCount);
    }

    [Fact]
    public async Task Probe_CurrentRoleDowngraded_DoesNotContactCandidate()
    {
        await using var current = await AdminGrpcFixture.StartAsync();
        await using var candidate = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(current);
        using var client = await AuthenticatedApiClient.CreateAsync(factory);
        current.Authority.Role = AccountType.GameMaster;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/configuration/test-connection",
            new { configuration = ConfigurationHttpFixtures.Candidate(candidate.Address), username = "Admin", password = "fixture-only" })).StatusCode);
        Assert.Equal(0, candidate.Authority.LoginCallCount);
    }
}
