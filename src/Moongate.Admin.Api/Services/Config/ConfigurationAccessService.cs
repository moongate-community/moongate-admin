using Moongate.Admin.Api.Interfaces.Configuration;
using Moongate.Admin.Api.Interfaces.Upstream;
using Moongate.Admin.Api.Internal;
using Moongate.Admin.Api.Services.Authentication;

namespace Moongate.Admin.Api.Services.Config;

public sealed class ConfigurationAccessService
{
    private readonly SetupTokenService _tokens;
    private readonly IConnectionCatalogStore _store;
    private readonly IMoongateAdminClient _client;
    private readonly AdminSessionAccessor _sessions;

    public ConfigurationAccessService(
        SetupTokenService tokens, IConnectionCatalogStore store, IMoongateAdminClient client, AdminSessionAccessor sessions
    )
    {
        _tokens = tokens;
        _store = store;
        _client = client;
        _sessions = sessions;
    }

    public void RequireSetupToken(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_tokens.Verify(context.Request.Headers["X-Moongate-Setup-Token"].ToString()))
        {
            throw new ConfigurationException(StatusCodes.Status401Unauthorized, "setup_token_required");
        }
    }

    public async Task RequireAdministratorAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var session = await _sessions.GetAsync(context, cancellationToken)
                      ?? throw new ConfigurationException(StatusCodes.Status401Unauthorized, "authentication_required");
        if (!context.User.IsInRole("administrator"))
        {
            throw new ConfigurationException(StatusCodes.Status403Forbidden, "permission_denied");
        }

        await _client.ListAccountsAsync(1, 0, session.AccessToken, cancellationToken);
    }

    public async Task RequireProbeAccessAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_store.Current.Configured && _tokens.Verify(context.Request.Headers["X-Moongate-Setup-Token"].ToString()))
        {
            return;
        }

        await RequireAdministratorAsync(context, cancellationToken);
    }
}
