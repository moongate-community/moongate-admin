using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;

namespace Moongate.Admin.Tests.TestSupport.Grpc;

public sealed class FakeAdminSessionService : AdminSession.AdminSessionBase
{
    private readonly FakeAdminAuthority _authority;

    public FakeAdminSessionService(FakeAdminAuthority authority)
    {
        _authority = authority;
    }

    public override Task<Empty> Logout(Empty request, ServerCallContext context)
    {
        if (_authority.Failure is { } failure)
        {
            throw new RpcException(new Status(failure, "upstream-private-detail"));
        }

        _authority.RemoveToken(
            context.RequestHeaders.GetValue("authorization")?.Replace("Bearer ", "", StringComparison.Ordinal)
        );
        return Task.FromResult(new Empty());
    }
}
