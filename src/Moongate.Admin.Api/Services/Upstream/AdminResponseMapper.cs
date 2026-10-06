using System.Globalization;
using Grpc.Core;
using Moongate.Admin.Api.Data.Accounts;
using Moongate.Admin.Api.Data.Servers;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Types.Accounts;
using Moongate.Admin.Api.Types.Servers;
using Wire = Moongate.Admin.Contracts.V1;

namespace Moongate.Admin.Api.Services.Upstream;

public static class AdminResponseMapper
{
    public static AccountSummaryResponse ToAccount(Wire.AccountSummary account)
    {
        if (account.AccountId == 0 || account.CreatedAt is null)
        {
            throw new UpstreamCallException(StatusCode.Internal);
        }

        return new AccountSummaryResponse
        {
            AccountId = account.AccountId, Username = account.Username,
            AccountType = account.AccountType switch
            {
                Wire.AccountType.Regular => AdminAccountType.Regular,
                Wire.AccountType.GameMaster => AdminAccountType.GameMaster,
                Wire.AccountType.Administrator => AdminAccountType.Administrator,
                _ => throw new UpstreamCallException(StatusCode.Internal)
            },
            CanAccessApi = account.CanAccessApi, IsLocked = account.IsLocked,
            CreatedAt = ToDate(account.CreatedAt)
        };
    }

    public static ServerInfoResponse ToServer(Wire.GetServerInfoResponse server)
    {
        return new ServerInfoResponse
        {
            Version = server.Version, Codename = server.Codename, InstanceId = server.InstanceId, RealmId = server.RealmId,
            Mode = server.Mode switch
            {
                Wire.ServerMode.Login => AdminServerMode.Login,
                Wire.ServerMode.Game => AdminServerMode.Game,
                Wire.ServerMode.Standalone => AdminServerMode.Standalone,
                _ => throw new UpstreamCallException(StatusCode.Internal)
            },
            UptimeSeconds = server.UptimeSeconds.ToString(CultureInfo.InvariantCulture)
        };
    }

    public static DateTimeOffset ToDate(Google.Protobuf.WellKnownTypes.Timestamp timestamp)
    {
        try
        {
            return timestamp.ToDateTimeOffset();
        }
        catch (InvalidOperationException)
        {
            throw new UpstreamCallException(StatusCode.Internal);
        }
    }
}
