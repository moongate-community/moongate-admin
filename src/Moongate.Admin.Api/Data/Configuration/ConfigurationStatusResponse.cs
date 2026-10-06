namespace Moongate.Admin.Api.Data.Configuration;

public sealed class ConfigurationStatusResponse
{
    public bool Configured { get; init; }
    public bool SetupAvailable { get; init; }
}
