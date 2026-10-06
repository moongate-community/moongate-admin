using Moongate.Admin.Api.Data.Accounts;
using Moongate.Admin.Api.Data.Configuration;
using Moongate.Admin.Api.Data.Internal.Configuration;
using Moongate.Admin.Api.Data.Internal.Sessions;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Upstream;
using Moongate.Admin.Api.Types.Accounts;
using Moongate.Admin.Api.Types.Servers;
using Serilog;

namespace Moongate.Admin.Api.Services.Config;

public sealed class ConnectionProbeService
{
    private static readonly TimeSpan LogoutTimeout = TimeSpan.FromSeconds(2);
    private readonly MoongateOptionsValidator _validator;
    private readonly IHttpClientFactory _clients;
    private readonly TimeProvider _clock;
    private readonly Serilog.ILogger _logger = Log.ForContext<ConnectionProbeService>();
    public ConnectionProbeService(MoongateOptionsValidator validator, IHttpClientFactory clients, TimeProvider clock)
    {
        _validator = validator; _clients = clients; _clock = clock;
    }
    public async Task<ConnectionProbeResponse> TestAsync(ConnectionProbeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Configuration is null || !_validator.Validate(null, request.Configuration).Succeeded)
        {
            throw new ConfigurationException(StatusCodes.Status400BadRequest, "configuration_invalid");
        }
        AdminRequestValidator.ValidateCredentials(request.Username, request.Password);
        var selected = request.EndpointId ?? request.Configuration.AuthenticationEndpointId;
        if (!request.Configuration.Endpoints.Any(endpoint => endpoint.Id == selected))
        {
            throw new ConfigurationException(StatusCodes.Status400BadRequest, "configuration_invalid");
        }
        using var candidate = new GrpcMoongateAdminClient(new ConnectionCatalogSnapshot(Guid.NewGuid().ToString("N"), request.Configuration), _clients, _clock);
        UpstreamLoginResult? login = null;
        try
        {
            login = await candidate.LoginAsync(new LoginRequest { Username = request.Username, Password = request.Password }, cancellationToken);
            if (login.Account.AccountType != AdminAccountType.Administrator || !login.Account.CanAccessApi || login.Account.IsLocked)
            {
                throw new ConfigurationException(StatusCodes.Status403Forbidden, "permission_denied");
            }
            var authentication = await candidate.GetServerInfoAsync(request.Configuration.AuthenticationEndpointId, login.AccessToken, cancellationToken);
            if (authentication.Mode is not AdminServerMode.Login and not AdminServerMode.Standalone)
            {
                throw new ConfigurationException(StatusCodes.Status400BadRequest, "configuration_invalid");
            }
            var server = selected == request.Configuration.AuthenticationEndpointId ? authentication
                : await candidate.GetServerInfoAsync(selected, login.AccessToken, cancellationToken);
            return new ConnectionProbeResponse { EndpointId = selected, Server = server };
        }
        catch (UpstreamCallException exception)
        {
            throw new UpstreamCallException(exception.StatusCode, exception.MutationOutcomeUnknown, invalidatesLocalSession: false);
        }
        finally
        {
            if (login is not null)
            {
                using var cleanup = new CancellationTokenSource(LogoutTimeout);
                try { await candidate.LogoutAsync(login.AccessToken, cleanup.Token); }
                catch (Exception exception) when (exception is UpstreamCallException or OperationCanceledException)
                {
                    _logger.Warning("Candidate connection session logout was not confirmed");
                }
            }
        }
    }
}
