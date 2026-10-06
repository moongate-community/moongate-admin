namespace Moongate.Admin.Tests.TestSupport.Configuration;

public sealed class TemporaryConfigurationDirectory : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "moongate-admin-tests", Guid.NewGuid().ToString("N"));
    public string FilePath { get; }

    public TemporaryConfigurationDirectory()
    {
        Directory.CreateDirectory(Root);
        FilePath = Path.Combine(Root, "connections.json");
    }

    public void Dispose()
    {
        Directory.Delete(Root, recursive: true);
    }
}
