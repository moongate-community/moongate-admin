namespace Moongate.Admin.Tests.TestSupport.Hosting;

public sealed class TemporaryFrontendDirectory : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "moongate-admin-frontend", Guid.NewGuid().ToString("N"));

    public TemporaryFrontendDirectory()
    {
        Directory.CreateDirectory(Path.Combine(Root, "assets"));
        File.WriteAllText(Path.Combine(Root, "index.html"), "<html><body>spa-index-marker</body></html>");
        File.WriteAllText(Path.Combine(Root, "assets", "app.js"), "console.log('asset-marker');");
    }

    public void Dispose()
    {
        Directory.Delete(Root, recursive: true);
    }
}
