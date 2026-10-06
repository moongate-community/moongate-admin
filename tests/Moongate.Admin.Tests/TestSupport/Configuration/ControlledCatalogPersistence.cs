using Moongate.Admin.Api.Data.Internal.Configuration;
using Moongate.Admin.Api.Interfaces.Configuration;

namespace Moongate.Admin.Tests.TestSupport.Configuration;

public sealed class ControlledCatalogPersistence : IConnectionCatalogPersistence
{
    private readonly IConnectionCatalogPersistence _inner;
    public Action? AfterCommit { get; set; }
    public bool FailWrite { get; set; }

    public ControlledCatalogPersistence(IConnectionCatalogPersistence inner)
    {
        _inner = inner;
    }

    public Task<PersistedConnectionCatalog?> ReadAsync(CancellationToken cancellationToken)
    {
        return _inner.ReadAsync(cancellationToken);
    }

    public async Task WriteAsync(PersistedConnectionCatalog catalog, CancellationToken cancellationToken)
    {
        if (FailWrite)
        {
            throw new IOException("private-filesystem-detail");
        }

        await _inner.WriteAsync(catalog, cancellationToken);
        AfterCommit?.Invoke();
    }
}
