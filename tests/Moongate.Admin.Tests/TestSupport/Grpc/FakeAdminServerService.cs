using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;

namespace Moongate.Admin.Tests.TestSupport.Grpc;

public sealed class FakeAdminServerService : AdminServer.AdminServerBase
{
    private readonly FakeAdminAuthority _authority;
    public FakeAdminServerService(FakeAdminAuthority authority)
    {
        _authority = authority;
    }
    public override Task<GetServerInfoResponse> GetServerInfo(Empty request, ServerCallContext context)
    {
        _authority.Check(context);
        return Task.FromResult(new GetServerInfoResponse
        {
            Version = "0.14.0", Codename = "fixture", InstanceId = _authority.InstanceId,
            RealmId = "realm-1", Mode = _authority.Mode, UptimeSeconds = ulong.MaxValue
        });
    }
}
