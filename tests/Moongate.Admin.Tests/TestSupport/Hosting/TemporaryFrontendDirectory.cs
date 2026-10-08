namespace Moongate.Admin.Tests.TestSupport.Hosting;

public sealed class TemporaryFrontendDirectory : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "moongate-frontend-tests-" + Guid.NewGuid().ToString("N"));

    public TemporaryFrontendDirectory(bool includeBuild = true)
    {
        Directory.CreateDirectory(Root);
        var frontendRoot = Path.Combine(Root, "frontend");
        Directory.CreateDirectory(frontendRoot);
        if (includeBuild)
        {
            File.WriteAllText(Path.Combine(frontendRoot, "index.html"), "<!doctype html><html><body>frontend-fixture</body></html>");
            Directory.CreateDirectory(Path.Combine(frontendRoot, "assets"));
            File.WriteAllText(Path.Combine(frontendRoot, "assets", "fixture.js"), "window.frontendFixture = true;");
        }
    }

    public void Dispose()
    {
        Directory.Delete(Root, true);
    }
}
