using Moongate.Admin.Api.Extensions;
using Serilog;

namespace Moongate.Admin.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSerilog(configuration => configuration.MinimumLevel.Warning().WriteTo.Console());
        builder.Services.AddMoongateAdmin(builder.Configuration, builder.Environment);
        var app = builder.Build();
        app.UseExceptionHandler();
        app.MapMoongateAdmin();
        await app.RunAsync();
    }
}
