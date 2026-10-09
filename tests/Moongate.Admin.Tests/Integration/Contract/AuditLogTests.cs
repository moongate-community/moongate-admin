using System.Net;
using Grpc.Core;
using Moongate.Admin.Tests.TestSupport.Authentication;
using Moongate.Admin.Tests.TestSupport.Grpc;
using Moongate.Admin.Tests.TestSupport.Hosting;

namespace Moongate.Admin.Tests.Integration.Contract;

[Collection("Logging")]
public class AuditLogTests
{
    [Fact]
    public async Task Audit_FailedRequest_NamesTheOperation()
    {
        await using var grpc = await AdminGrpcFixture.StartAsync();
        await using var factory = new AdminApiFactory();
        factory.UseGrpc(grpc);
        var client = await AuthenticatedApiClient.CreateAsync(factory);
        grpc.Authority.Failure = StatusCode.Unavailable;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/auth/session")).StatusCode);
        var entry = Assert.Single(
            factory.Logs.Events,
            item => item.MessageTemplate.Text.Contains("Administration operation") &&
                    item.RenderMessage().Contains("status 503")
        );
        var operation = entry.Properties["Operation"].ToString();
        Assert.Contains("GET", operation);
        Assert.Contains("/api/auth/session", operation);
        Assert.DoesNotContain("unmapped", operation);
    }
}
