using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;

namespace Moongate.Admin.Tests.TestSupport.Grpc;

public sealed class FakeAdminLoginService : AdminLogin.AdminLoginBase
{
    private readonly FakeAdminAuthority _authority;
    public FakeAdminLoginService(FakeAdminAuthority authority)
    {
        _authority = authority;
    }
    public override Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        _authority.LoginCallCount++;
        _authority.LastLogin = request;
        _authority.LastDeadline = context.Deadline;
        if (_authority.Failure is { } failure)
        {
            throw new RpcException(new Status(failure, "upstream-private-detail"));
        }
        _authority.Revoked = false;
        return Task.FromResult(new LoginResponse
        {
            Account = _authority.Summary(request.Username), AccessToken = _authority.IssueToken(),
            ExpiresAt = Timestamp.FromDateTimeOffset(_authority.ExpiresAt)
        });
    }
}
