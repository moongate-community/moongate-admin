using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Moongate.Admin.Api.Data.Internal.Configuration;
using Moongate.Admin.Api.Data.Accounts;
using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Upstream;
using Moongate.Admin.Tests.TestSupport.Grpc;

namespace Moongate.Admin.Tests.Integration.Upstream;

public class GrpcTlsTests
{
    [Theory]
    [InlineData(true, "localhost", true)]
    [InlineData(false, "localhost", false)]
    [InlineData(true, "127.0.0.1", false)]
    public async Task Login_CertificateTrustAndHostname_Enforced(bool trusted, string host, bool succeeds)
    {
        using var certificates = new TestGrpcCertificates();
        await using var fixture = await AdminGrpcFixture.StartAsync(certificate: certificates.Server);
        var services = new ServiceCollection();
        var registration = services.AddHttpClient("MoongateAdmin");
        if (trusted)
        {
            registration.ConfigurePrimaryHttpMessageHandler(certificates.TrustedHandler);
        }

        await using var provider = services.BuildServiceProvider();
        var uri = new UriBuilder(fixture.Address) { Host = host };
        using var client = new GrpcMoongateAdminClient(
            new ConnectionCatalogSnapshot(
                "fixture",
                new MoongateOptions
                {
                    AuthenticationEndpointId = "login",
                    Endpoints = [new MoongateEndpointOptions { Id = "login", Label = "Login", Address = uri.Uri.ToString() }]
                }
            ),
            provider.GetRequiredService<IHttpClientFactory>(),
            TimeProvider.System
        );
        if (succeeds)
        {
            var result = await client.LoginAsync(
                new LoginRequest { Username = "Admin", Password = "fixture-only" },
                CancellationToken.None
            );
            Assert.Equal((uint)7, result.Account.AccountId);
        }
        else
        {
            var exception = await Assert.ThrowsAsync<UpstreamCallException>(() => client.LoginAsync(
                    new LoginRequest
                    {
                        Username = "Admin", Password = "fixture-only"
                    },
                    CancellationToken.None
                )
            );
            Assert.Equal(StatusCode.Unavailable, exception.StatusCode);
            Assert.Equal(0, fixture.Authority.LoginCallCount);
        }
    }
}
