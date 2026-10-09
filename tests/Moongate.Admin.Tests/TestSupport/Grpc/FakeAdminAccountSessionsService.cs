using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;

namespace Moongate.Admin.Tests.TestSupport.Grpc;

public sealed class FakeAdminAccountSessionsService : AdminAccountSessions.AdminAccountSessionsBase
{
    private readonly FakeAdminAuthority _authority;

    public FakeAdminAccountSessionsService(FakeAdminAuthority authority)
    {
        _authority = authority;
    }

    public override Task<Empty> RevokeAccountSessions(RevokeAccountSessionsRequest request, ServerCallContext context)
    {
        _authority.Check(context, true);
        _authority.LastRevokedAccount = request.AccountId;
        if (request.AccountId == 999)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "missing"));
        }

        if (request.AccountId == 7)
        {
            _authority.RevokeAll();
        }

        return Task.FromResult(new Empty());
    }
}
