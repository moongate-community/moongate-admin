using Moongate.Admin.Api.Data.Config;

namespace Moongate.Admin.Api.Data.Internal.Configuration;

public sealed class PersistedConnectionCatalog
{
    public required int SchemaVersion { get; init; }
    public required string Revision { get; init; }
    public required MoongateOptions Configuration { get; init; }
}
