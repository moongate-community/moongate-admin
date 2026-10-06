namespace Moongate.Admin.Api.Data.Servers;

public sealed class ServerInfoResponse
{
    public string Version { get; init; } = "";
    public string Codename { get; init; } = "";
    public string InstanceId { get; init; } = "";
    public string RealmId { get; init; } = "";
    public Types.Servers.AdminServerMode Mode { get; init; }
    public string UptimeSeconds { get; init; } = "0";
}
