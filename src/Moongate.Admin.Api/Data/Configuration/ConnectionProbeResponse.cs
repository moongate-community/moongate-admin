using Moongate.Admin.Api.Data.Servers;

namespace Moongate.Admin.Api.Data.Configuration;

public sealed class ConnectionProbeResponse
{
    public required string EndpointId { get; init; }
    public required ServerInfoResponse Server { get; init; }
}
