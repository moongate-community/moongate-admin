using Moongate.Admin.Api.Data.Accounts;
using Moongate.Admin.Api.Data.Internal.Sessions;
using Moongate.Admin.Api.Data.Servers;

namespace Moongate.Admin.Api.Interfaces.Upstream;

/// <summary>
///     Calls the configured Moongate administration services without exposing wire types to HTTP routes.
/// </summary>
public interface IMoongateAdminClient
{
    /// <summary>
    ///     Logs in through the configured authentication endpoint.
    /// </summary>
    Task<UpstreamLoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Revokes the presented upstream session.
    /// </summary>
    Task LogoutAsync(string accessToken, CancellationToken cancellationToken);

    /// <summary>
    ///     Reads information from a configured server ID.
    /// </summary>
    Task<ServerInfoResponse> GetServerInfoAsync(string serverId, string accessToken, CancellationToken cancellationToken);

    /// <summary>
    ///     Reads one account page from the authentication endpoint.
    /// </summary>
    Task<AccountPageResponse> ListAccountsAsync(
        uint pageSize, uint afterAccountId, string accessToken, CancellationToken cancellationToken
    );

    /// <summary>
    ///     Creates an account once, without automatic mutation retries.
    /// </summary>
    Task<AccountSummaryResponse> CreateAccountAsync(
        CreateAccountRequest request, string accessToken, CancellationToken cancellationToken
    );

    /// <summary>
    ///     Revokes an account's administrative sessions.
    /// </summary>
    Task RevokeAccountSessionsAsync(uint accountId, string accessToken, CancellationToken cancellationToken);
}
