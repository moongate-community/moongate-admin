using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Data.Internal.Configuration;

namespace Moongate.Admin.Api.Data.Configuration;

public sealed class ConnectionConfigurationResponse
{
    public string AuthenticationEndpointId { get; init; } = "";
    public bool AllowInsecureLoopback { get; init; }
    public IReadOnlyList<MoongateEndpointOptions> Endpoints { get; init; } = [];
    public string Revision { get; init; } = "";
    public bool ReauthenticationRequired { get; init; }

    public static ConnectionConfigurationResponse FromSnapshot(
        ConnectionCatalogSnapshot snapshot, bool reauthenticationRequired = false
    )
    {
        return new ConnectionConfigurationResponse
        {
            AuthenticationEndpointId = snapshot.AuthenticationEndpointId,
            AllowInsecureLoopback = snapshot.AllowInsecureLoopback, Endpoints = snapshot.ToOptions().Endpoints,
            Revision = snapshot.Revision, ReauthenticationRequired = reauthenticationRequired
        };
    }
}
