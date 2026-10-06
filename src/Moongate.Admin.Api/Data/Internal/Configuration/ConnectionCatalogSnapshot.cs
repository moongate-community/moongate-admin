using Moongate.Admin.Api.Data.Config;

namespace Moongate.Admin.Api.Data.Internal.Configuration;

public sealed class ConnectionCatalogSnapshot
{
    public string Revision { get; }
    public string AuthenticationEndpointId { get; }
    public bool AllowInsecureLoopback { get; }
    public IReadOnlyList<ConnectionEndpoint> Endpoints { get; }
    public bool Configured { get { return Endpoints.Count > 0; } }
    public ConnectionCatalogSnapshot(string revision, MoongateOptions configuration)
    {
        Revision = revision;
        AuthenticationEndpointId = configuration.AuthenticationEndpointId;
        AllowInsecureLoopback = configuration.AllowInsecureLoopback;
        Endpoints = Array.AsReadOnly(configuration.Endpoints.Select(endpoint => new ConnectionEndpoint(endpoint.Id, endpoint.Label, endpoint.Address)).ToArray());
    }
    public MoongateOptions ToOptions()
    {
        return new MoongateOptions { AuthenticationEndpointId = AuthenticationEndpointId, AllowInsecureLoopback = AllowInsecureLoopback,
            Endpoints = Endpoints.Select(endpoint => new MoongateEndpointOptions { Id = endpoint.Id, Label = endpoint.Label, Address = endpoint.Address }).ToArray() };
    }
}
