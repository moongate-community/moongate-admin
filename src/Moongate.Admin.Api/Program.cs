var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
await app.RunAsync();

public partial class Program
{
}
