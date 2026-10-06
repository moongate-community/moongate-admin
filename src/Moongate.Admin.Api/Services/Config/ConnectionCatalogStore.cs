using Moongate.Admin.Api.Data.Config;
using Moongate.Admin.Api.Data.Internal.Configuration;
using Moongate.Admin.Api.Interfaces.Configuration;
using Moongate.Admin.Api.Internal;
using System.Text.Json;

namespace Moongate.Admin.Api.Services.Config;

public sealed class ConnectionCatalogStore : IConnectionCatalogStore, IHostedService, IDisposable
{
    private const int SchemaVersion = 1;
    private readonly IConfiguration _configuration;
    private readonly IConnectionCatalogPersistence _persistence;
    private readonly MoongateOptionsValidator _validator;
    private readonly SemaphoreSlim _writes = new(1, 1);
    private ConnectionCatalogSnapshot? _current;
    public ConnectionCatalogSnapshot Current
    {
        get { return Volatile.Read(ref _current) ?? throw new InvalidOperationException("Configuration is not initialized."); }
    }
    public ConnectionCatalogStore(IConfiguration configuration, IConnectionCatalogPersistence persistence, MoongateOptionsValidator validator)
    {
        _configuration = configuration; _persistence = persistence; _validator = validator;
    }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_current is not null) { return; }
        try
        {
            var saved = await _persistence.ReadAsync(cancellationToken);
            if (saved is not null)
            {
                if (saved.SchemaVersion != SchemaVersion || !Guid.TryParseExact(saved.Revision, "N", out _) ||
                    saved.Configuration is null || !_validator.Validate(null, saved.Configuration).Succeeded)
                {
                    throw new ConfigurationException(StatusCodes.Status500InternalServerError, "configuration_load_failed");
                }
                Volatile.Write(ref _current, new ConnectionCatalogSnapshot(saved.Revision, saved.Configuration));
                return;
            }
            var options = new MoongateOptions();
            _configuration.GetSection("Moongate").Bind(options);
            var empty = options.Endpoints is { Count: 0 } && options.AuthenticationEndpointId == "";
            if (!empty && !_validator.Validate(null, options).Succeeded)
            {
                throw new ConfigurationException(StatusCodes.Status500InternalServerError, "configuration_load_failed");
            }
            Volatile.Write(ref _current, new ConnectionCatalogSnapshot(Guid.NewGuid().ToString("N"), options));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException)
        {
            throw new ConfigurationException(StatusCodes.Status500InternalServerError, "configuration_load_failed");
        }
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    public Task<ConnectionCatalogSnapshot> SetupAsync(MoongateOptions configuration, CancellationToken cancellationToken)
    {
        return SaveAsync(configuration, null, cancellationToken);
    }
    public Task<ConnectionCatalogSnapshot> ReplaceAsync(MoongateOptions configuration, string expectedRevision, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(expectedRevision);
        return SaveAsync(configuration, expectedRevision, cancellationToken);
    }
    private async Task<ConnectionCatalogSnapshot> SaveAsync(MoongateOptions configuration, string? expectedRevision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!_validator.Validate(null, configuration).Succeeded)
        {
            throw new ConfigurationException(StatusCodes.Status400BadRequest, "configuration_invalid");
        }
        var candidate = new ConnectionCatalogSnapshot(Guid.NewGuid().ToString("N"), configuration);
        await _writes.WaitAsync(cancellationToken);
        try
        {
            if (expectedRevision is null && Current.Configured)
            {
                throw new ConfigurationException(StatusCodes.Status409Conflict, "configuration_already_configured");
            }
            if (expectedRevision is not null && !string.Equals(expectedRevision, Current.Revision, StringComparison.Ordinal))
            {
                throw new ConfigurationException(StatusCodes.Status412PreconditionFailed, "configuration_changed");
            }
            try
            {
                await _persistence.WriteAsync(new PersistedConnectionCatalog
                { SchemaVersion = SchemaVersion, Revision = candidate.Revision, Configuration = candidate.ToOptions() }, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                throw new ConfigurationException(StatusCodes.Status500InternalServerError, "configuration_save_failed");
            }
            Volatile.Write(ref _current, candidate);
            return candidate;
        }
        finally { _writes.Release(); }
    }
    public void Dispose()
    {
        _writes.Dispose();
    }
}
