using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Data.Internal.Configuration;

namespace Moongate.Admin.Api.Interfaces.Configuration;

/// <summary>
///     Owns the current immutable catalog and serialized durable replacements.
/// </summary>
public interface IConnectionCatalogStore
{
    /// <summary>
    ///     Gets the last successfully committed snapshot.
    /// </summary>
    ConnectionCatalogSnapshot Current { get; }
    /// <summary>
    ///     Saves the first configuration only when no catalog is configured.
    /// </summary>
    Task<ConnectionCatalogSnapshot> SetupAsync(MoongateOptions configuration, CancellationToken cancellationToken);
    /// <summary>
    ///     Replaces the catalog only when the supplied revision still matches.
    /// </summary>
    Task<ConnectionCatalogSnapshot> ReplaceAsync(MoongateOptions configuration, string expectedRevision, CancellationToken cancellationToken);
}
