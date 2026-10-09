namespace Moongate.Admin.Api.Data.Config;

public sealed class MoongateOptions
{
    public string AuthenticationEndpointId { get; set; } = "";
    public bool AllowInsecureLoopback { get; set; }
    public IReadOnlyList<MoongateEndpointOptions> Endpoints { get; set; } = [];
}
