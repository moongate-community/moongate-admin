using Moongate.Admin.Api.Data.Internal.Configuration;
using Moongate.Admin.Api.Interfaces.Configuration;
using System.Text.Json;
using Serilog;

namespace Moongate.Admin.Api.Services.Config;

public sealed class FileConnectionCatalogPersistence : IConnectionCatalogPersistence
{
    private const int MaximumDocumentBytes = 65536;
    private readonly string _path;

    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, RespectNullableAnnotations = true
    };

    private readonly Serilog.ILogger _logger = Log.ForContext<FileConnectionCatalogPersistence>();

    public FileConnectionCatalogPersistence(string path)
    {
        _path = Path.GetFullPath(path);
    }

    public async Task<PersistedConnectionCatalog?> ReadAsync(CancellationToken cancellationToken)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(
                _path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                MaximumDocumentBytes,
                useAsync: true
            );
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        await using (stream)
        {
            if (stream.Length > MaximumDocumentBytes)
            {
                throw new IOException("Invalid configuration document size.");
            }

            return await JsonSerializer.DeserializeAsync<PersistedConnectionCatalog>(stream, _json, cancellationToken)
                   ?? throw new JsonException("Invalid configuration document.");
        }
    }

    public async Task WriteAsync(PersistedConnectionCatalog catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(_path) ?? throw new IOException("Invalid storage directory.");
        Directory.CreateDirectory(directory);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                             temporary,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             MaximumDocumentBytes,
                             useAsync: true
                         ))
            {
                await JsonSerializer.SerializeAsync(stream, catalog, _json, cancellationToken);
                if (stream.Length > MaximumDocumentBytes)
                {
                    throw new IOException("Invalid configuration document size.");
                }

                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.Warning("Configuration temporary file cleanup was not confirmed");
            }
        }
    }
}
