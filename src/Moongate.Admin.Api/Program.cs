using Moongate.Admin.Api.Extensions;
using Moongate.Admin.Api.Services.Errors;
using Moongate.Admin.Api.Services.Http;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog((services, configuration) => configuration.MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .ReadFrom.Services(services)
    .WriteTo.Console()
);
builder.Services.AddMoongateAdmin(builder.Configuration, builder.Environment);
var app = builder.Build();
app.UseMiddleware<AdminRequestAuditMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<ApiTransportMiddleware>();
app.UseDevelopmentSwagger();
app.UseAuthentication();
app.UseAuthorization();
app.MapMoongateAdmin();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapFallback(
        "/{**path}",
        context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }
    )
    .AllowAnonymous()
    .ExcludeFromDescription();
await app.RunAsync();

public partial class Program
{
}
