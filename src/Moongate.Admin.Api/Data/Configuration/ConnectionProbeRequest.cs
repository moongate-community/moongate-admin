using Moongate.Admin.Api.Data.Config;

namespace Moongate.Admin.Api.Data.Configuration;

public sealed class ConnectionProbeRequest
{
    public required MoongateOptions Configuration { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public string? EndpointId { get; init; }
}
