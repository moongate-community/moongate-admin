using System.Security.Cryptography;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.WebUtilities;
using Moongate.Admin.Contracts.V1;

namespace Moongate.Admin.Tests.TestSupport.Grpc;

public sealed class FakeAdminAuthority
{
    public string Token { get; } = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    public AccountType Role { get; set; } = AccountType.Administrator;
    public bool Revoked { get; set; }
    public StatusCode? Failure { get; set; }
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddMinutes(30);
    public LoginRequest? LastLogin { get; set; }
    public ListAccountsRequest? LastList { get; set; }
    public CreateAccountRequest? LastCreate { get; set; }
    public uint LastRevokedAccount { get; set; }
    public DateTime LastDeadline { get; set; }
    public string? LastAuthorization { get; set; }
    public int LoginCallCount { get; set; }
    public int CreateCallCount { get; set; }
    public int ListCallCount { get; set; }
    public bool LoseCreateResponse { get; set; }
    public string InstanceId { get; set; } = "fixture-login";
    public ServerMode Mode { get; set; } = ServerMode.Login;
    public AccountSummary? CreatedAccount { get; set; }

    public AccountSummary Summary(string username = "Admin", uint id = 7)
    {
        return new AccountSummary
        {
            AccountId = id, Username = username, AccountType = Role,
            CanAccessApi = true, IsLocked = false, CreatedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.Parse("2026-01-01T00:00:00Z"))
        };
    }
    public void Check(ServerCallContext context, bool administrator = false)
    {
        LastAuthorization = context.RequestHeaders.GetValue("authorization");
        LastDeadline = context.Deadline;
        if (Failure is { } failure)
        {
            throw new RpcException(new Status(failure, "upstream-private-detail"));
        }
        if (Revoked || LastAuthorization != "Bearer " + Token)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "revoked"));
        }
        if (administrator && Role != AccountType.Administrator)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "denied"));
        }
    }
    public void RevokeIssuedToken()
    {
        Revoked = true;
    }
}
