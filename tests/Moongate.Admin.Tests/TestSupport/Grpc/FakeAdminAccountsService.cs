using Grpc.Core;
using Moongate.Admin.Contracts.V1;

namespace Moongate.Admin.Tests.TestSupport.Grpc;

public sealed class FakeAdminAccountsService : AdminAccounts.AdminAccountsBase
{
    private readonly FakeAdminAuthority _authority;

    public FakeAdminAccountsService(FakeAdminAuthority authority)
    {
        _authority = authority;
    }

    public override Task<ListAccountsResponse> ListAccounts(ListAccountsRequest request, ServerCallContext context)
    {
        _authority.ListCallCount++;
        _authority.Check(context, true);
        _authority.LastList = request;
        var response = new ListAccountsResponse { NextAfterAccountId = request.AfterAccountId == 0 ? uint.MaxValue : 0 };
        response.Accounts.Add(_authority.CreatedAccount ?? _authority.Summary());
        return Task.FromResult(response);
    }

    public override Task<AccountSummary> CreateAccount(CreateAccountRequest request, ServerCallContext context)
    {
        _authority.CreateCallCount++;
        _authority.Check(context, true);
        _authority.LastCreate = request;
        var summary = _authority.Summary(request.Username, 8);
        summary.AccountType = request.HasAccountType ? request.AccountType : AccountType.Regular;
        summary.CanAccessApi = request.CanAccessApi;
        _authority.CreatedAccount = summary;
        if (_authority.LoseCreateResponse)
        {
            throw new RpcException(new Status(StatusCode.DeadlineExceeded, "response lost after commit"));
        }

        return Task.FromResult(summary);
    }
}
