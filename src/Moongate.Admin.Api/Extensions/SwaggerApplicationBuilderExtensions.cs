namespace Moongate.Admin.Api.Extensions;

public static class SwaggerApplicationBuilderExtensions
{
    public static WebApplication UseDevelopmentSwagger(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.UseSwaggerUI(options =>
            {
                options.RoutePrefix = "swagger";
                options.DocumentTitle = "Moongate Admin API";
                options.SwaggerEndpoint("../openapi/v1.json", "Moongate Admin API v1");
                options.ConfigObject.PersistAuthorization = false;
                options.ConfigObject.ValidatorUrl = null;
            }
        );
        return app;
    }
}
