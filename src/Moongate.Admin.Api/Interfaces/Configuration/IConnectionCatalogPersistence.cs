using Moongate.Admin.Api.Data.Internal.Configuration;

namespace Moongate.Admin.Api.Interfaces.Configuration;

/// <summary>
///     Reads and atomically commits the non-secret connection catalog.
/// </summary>
public interface IConnectionCatalogPersistence
{
    /// <summary>
    ///     Reads the saved document, returning null only when it is absent.
    /// </summary>
    Task<PersistedConnectionCatalog?> ReadAsync(CancellationToken cancellationToken);
    /// <summary>
    ///     Commits the document and does not report cancellation after the rename.
    /// </summary>
    Task WriteAsync(PersistedConnectionCatalog catalog, CancellationToken cancellationToken);
}
