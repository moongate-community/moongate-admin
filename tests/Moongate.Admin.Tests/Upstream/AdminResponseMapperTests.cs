using Google.Protobuf.WellKnownTypes;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Upstream;
using Moongate.Admin.Api.Types.Accounts;
using Moongate.Admin.Api.Types.Servers;
using Moongate.Admin.Contracts.V1;

namespace Moongate.Admin.Tests.Upstream;

public class AdminResponseMapperTests
{
    private static AccountSummary Wire()
    {
        return new AccountSummary
        {
            AccountId = 7, Username = "Admin", AccountType = AccountType.Administrator, CanAccessApi = true,
            CreatedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.Parse("2026-01-01T00:00:00Z"))
        };
    }

    [Fact]
    public void ToAccount_Valid_MapsAllFields()
    {
        var account = AdminResponseMapper.ToAccount(Wire());
        Assert.Equal((uint)7, account.AccountId);
        Assert.Equal(AdminAccountType.Administrator, account.AccountType);
        Assert.True(account.CanAccessApi);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), account.CreatedAt);
    }

    [Fact]
    public void ToAccount_UnknownRole_Rejects()
    {
        var account = Wire();
        account.AccountType = (AccountType)99;
        Assert.Throws<UpstreamCallException>(() => AdminResponseMapper.ToAccount(account));
    }

    [Fact]
    public void ToAccount_ZeroIdOrMissingTimestamp_Rejects()
    {
        var zero = Wire();
        zero.AccountId = 0;
        Assert.Throws<UpstreamCallException>(() => AdminResponseMapper.ToAccount(zero));
        var missing = Wire();
        missing.CreatedAt = null;
        Assert.Throws<UpstreamCallException>(() => AdminResponseMapper.ToAccount(missing));
    }

    [Fact]
    public void ToDate_InvalidTimestamp_Rejects()
    {
        Assert.Throws<UpstreamCallException>(() => AdminResponseMapper.ToDate(new Timestamp { Seconds = long.MaxValue }));
    }

    [Fact]
    public void ToServer_MapsModeAndUnsignedUptime()
    {
        var server = AdminResponseMapper.ToServer(
            new GetServerInfoResponse { Mode = ServerMode.Standalone, UptimeSeconds = ulong.MaxValue }
        );
        Assert.Equal(AdminServerMode.Standalone, server.Mode);
        Assert.Equal("18446744073709551615", server.UptimeSeconds);
    }

    [Fact]
    public void ToServer_UnknownMode_Rejects()
    {
        Assert.Throws<UpstreamCallException>(() =>
            AdminResponseMapper.ToServer(new GetServerInfoResponse { Mode = (ServerMode)99 })
        );
    }
}
