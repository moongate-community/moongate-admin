using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using Moongate.Admin.Api.Data.Accounts;
using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Data.Internal.Sessions;
using Moongate.Admin.Api.Data.Servers;
using Moongate.Admin.Api.Interfaces.Upstream;
using Moongate.Admin.Api.Internal;
using Wire = Moongate.Admin.Contracts.V1;
using Moongate.Admin.Api.Data.Internal.Configuration;

namespace Moongate.Admin.Api.Services.Upstream;

public sealed class GrpcMoongateAdminClient : IMoongateAdminClient, IDisposable
{
    private const uint DefaultPageSize = 50;
    private static readonly TimeSpan CallDeadline = TimeSpan.FromSeconds(10);
    private readonly Dictionary<string, GrpcChannel> _channels;
    private readonly string _authenticationEndpoint;
    private readonly TimeProvider _clock;
    private readonly bool _configured;

    public GrpcMoongateAdminClient(ConnectionCatalogSnapshot snapshot, IHttpClientFactory clients, TimeProvider clock)
    {
        _clock = clock;
        _configured = snapshot.Configured;
        _authenticationEndpoint = snapshot.AuthenticationEndpointId;
        _channels = snapshot.Endpoints.ToDictionary(
            endpoint => endpoint.Id,
            endpoint => GrpcChannel.ForAddress(
                endpoint.Address,
                new GrpcChannelOptions
                {
                    HttpClient = clients.CreateClient("MoongateAdmin"), DisposeHttpClient = true
                }
            ),
            StringComparer.Ordinal
        );
    }

    public async Task<UpstreamLoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        AdminRequestValidator.ValidateCredentials(request.Username, request.Password);
        var response = await InvokeAsync(
            () => new Wire.AdminLogin.AdminLoginClient(Channel(_authenticationEndpoint)).LoginAsync(
                new Wire.LoginRequest { Username = request.Username, Password = request.Password },
                Options(null, cancellationToken)
            ),
            cancellationToken
        );
        if (response.Account is null || response.ExpiresAt is null || string.IsNullOrWhiteSpace(response.AccessToken))
        {
            throw new UpstreamCallException(StatusCode.Internal);
        }

        var expiry = AdminResponseMapper.ToDate(response.ExpiresAt);
        if (expiry <= _clock.GetUtcNow())
        {
            throw new UpstreamCallException(StatusCode.Internal);
        }

        return new UpstreamLoginResult
        {
            Account = AdminResponseMapper.ToAccount(response.Account), AccessToken = response.AccessToken, ExpiresAt = expiry
        };
    }

    public async Task LogoutAsync(string accessToken, CancellationToken cancellationToken)
    {
        await InvokeAsync(
            () => new Wire.AdminSession.AdminSessionClient(Channel(_authenticationEndpoint)).LogoutAsync(
                new Empty(),
                Options(accessToken, cancellationToken)
            ),
            cancellationToken
        );
    }

    public async Task<ServerInfoResponse> GetServerInfoAsync(
        string serverId, string accessToken, CancellationToken cancellationToken
    )
    {
        var response = await InvokeAsync(
            () => new Wire.AdminServer.AdminServerClient(Channel(serverId)).GetServerInfoAsync(
                new Empty(),
                Options(accessToken, cancellationToken)
            ),
            cancellationToken
        );
        return AdminResponseMapper.ToServer(response);
    }

    public async Task<AccountPageResponse> ListAccountsAsync(
        uint pageSize, uint afterAccountId, string accessToken, CancellationToken cancellationToken
    )
    {
        AdminRequestValidator.ValidatePagination(pageSize);
        var response = await InvokeAsync(
            () => new Wire.AdminAccounts.AdminAccountsClient(Channel(_authenticationEndpoint)).ListAccountsAsync(
                new Wire.ListAccountsRequest
                { PageSize = pageSize == 0 ? DefaultPageSize : pageSize, AfterAccountId = afterAccountId },
                Options(accessToken, cancellationToken)
            ),
            cancellationToken
        );
        return new AccountPageResponse
        {
            Accounts = response.Accounts.Select(AdminResponseMapper.ToAccount).ToArray(),
            NextAfterAccountId = response.NextAfterAccountId
        };
    }

    public async Task<AccountSummaryResponse> CreateAccountAsync(
        CreateAccountRequest request, string accessToken, CancellationToken cancellationToken
    )
    {
        AdminRequestValidator.ValidateAccountCreation(request);
        var wire = new Wire.CreateAccountRequest
        { Username = request.Username, Password = request.Password, CanAccessApi = request.CanAccessApi };
        if (request.AccountType is { } role)
        {
            wire.AccountType = role switch
            {
                Types.Accounts.AdminAccountType.Regular => Wire.AccountType.Regular,
                Types.Accounts.AdminAccountType.GameMaster => Wire.AccountType.GameMaster,
                Types.Accounts.AdminAccountType.Administrator => Wire.AccountType.Administrator,
                _ => throw new BadHttpRequestException("Invalid account type.")
            };
        }

        var response = await InvokeAsync(
            () => new Wire.AdminAccounts.AdminAccountsClient(Channel(_authenticationEndpoint)).CreateAccountAsync(
                wire,
                Options(accessToken, cancellationToken)
            ),
            cancellationToken,
            mutation: true
        );
        try
        {
            return AdminResponseMapper.ToAccount(response);
        }
        catch (UpstreamCallException exception)
        {
            throw new UpstreamCallException(exception.StatusCode, mutationOutcomeUnknown: true);
        }
    }

    public async Task RevokeAccountSessionsAsync(uint accountId, string accessToken, CancellationToken cancellationToken)
    {
        AdminRequestValidator.ValidateAccountId(accountId);
        await InvokeAsync(
            () => new Wire.AdminAccountSessions.AdminAccountSessionsClient(Channel(_authenticationEndpoint))
                .RevokeAccountSessionsAsync(
                    new Wire.RevokeAccountSessionsRequest { AccountId = accountId },
                    Options(accessToken, cancellationToken)
                ),
            cancellationToken
        );
    }

    private GrpcChannel Channel(string serverId)
    {
        if (!_configured)
        {
            throw new ConfigurationException(StatusCodes.Status503ServiceUnavailable, "configuration_required");
        }

        if (!_channels.TryGetValue(serverId, out var channel))
        {
            throw new BadHttpRequestException("Server not found.", StatusCodes.Status404NotFound);
        }

        return channel;
    }

    private static CallOptions Options(string? token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var headers = new Metadata();
        if (token is not null)
        {
            headers.Add("authorization", "Bearer " + token);
        }

        return new CallOptions(headers, DateTime.UtcNow.Add(CallDeadline), cancellationToken);
    }

    private static async Task<T> InvokeAsync<T>(
        Func<AsyncUnaryCall<T>> call, CancellationToken cancellationToken, bool mutation = false
    )
    {
        try
        {
            using var operation = call();
            return await operation.ResponseAsync;
        }
        catch (RpcException exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = exception.Status.DebugException is HttpRequestException
                ? StatusCode.Unavailable
                : exception.StatusCode;
            var uncertain = mutation && status is StatusCode.DeadlineExceeded or StatusCode.Unavailable
                or StatusCode.Cancelled or StatusCode.Unknown or StatusCode.Internal;
            throw new UpstreamCallException(status, uncertain);
        }
    }

    public void Dispose()
    {
        foreach (var channel in _channels.Values)
        {
            channel.Dispose();
        }
    }
}
