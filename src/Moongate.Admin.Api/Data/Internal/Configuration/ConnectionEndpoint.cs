namespace Moongate.Admin.Api.Data.Internal.Configuration;

public sealed class ConnectionEndpoint
{
    public string Id { get; }
    public string Label { get; }
    public string Address { get; }

    public ConnectionEndpoint(string id, string label, string address)
    {
        Id = id;
        Label = label;
        Address = address;
    }
}
