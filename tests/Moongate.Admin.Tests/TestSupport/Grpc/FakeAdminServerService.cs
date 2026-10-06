using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;

namespace Moongate.Admin.Tests.TestSupport.Grpc;

public sealed class FakeAdminServerService : AdminServer.AdminServerBase
{
    private readonly FakeAdminAuthority _authority;
    private readonly FakeAdminHostState _state;

    public FakeAdminServerService(FakeAdminAuthority authority, FakeAdminHostState state)
    {
        _authority = authority;
        _state = state;
    }

    public override async Task<GetServerInfoResponse> GetServerInfo(Empty request, ServerCallContext context)
    {
        _authority.Check(context);
        if (_authority.InformationEntered is not null && _authority.InformationRelease is not null)
        {
            _authority.InformationEntered.TrySetResult();
            await _authority.InformationRelease.Task.WaitAsync(context.CancellationToken);
        }

        return new GetServerInfoResponse
        {
            Version = "0.14.0", Codename = "fixture", InstanceId = _state.InstanceId,
            RealmId = "realm-1", Mode = _state.ReportedMode ?? _state.Mode, UptimeSeconds = ulong.MaxValue
        };
    }
}
