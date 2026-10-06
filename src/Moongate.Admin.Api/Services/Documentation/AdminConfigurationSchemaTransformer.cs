using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Moongate.Admin.Api.Data.Config;

namespace Moongate.Admin.Api.Services.Documentation;

public sealed class AdminConfigurationSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken
    )
    {
        if (context.JsonTypeInfo.Type == typeof(MoongateOptions))
        {
            schema.Required = new HashSet<string> { "authenticationEndpointId", "endpoints" };
            if (schema.Properties?["endpoints"] is OpenApiSchema endpoints)
            {
                endpoints.MinItems = 1;
                endpoints.MaxItems = 16;
                endpoints.Description = "Complete catalog replacement; unique case-sensitive endpoint IDs.";
            }

            if (schema.Properties?["authenticationEndpointId"] is OpenApiSchema authentication)
            {
                authentication.MinLength = 1;
                authentication.MaxLength = 64;
                authentication.Description = "ID in this catalog of the Login or Standalone authentication server.";
            }
        }

        if (context.JsonTypeInfo.Type == typeof(MoongateEndpointOptions))
        {
            schema.Required = new HashSet<string> { "id", "label", "address" };
            if (schema.Properties?["id"] is OpenApiSchema id)
            {
                id.MinLength = 1;
                id.MaxLength = 64;
                id.Pattern = "^[A-Za-z0-9._-]+$";
            }

            if (schema.Properties?["label"] is OpenApiSchema label)
            {
                label.MinLength = 1;
                label.MaxLength = 100;
                label.Description = "Nonblank, at most 100 UTF-16 code units, without control characters.";
            }

            if (schema.Properties?["address"] is OpenApiSchema address)
            {
                address.MaxLength = 2048;
                address.Description =
                    "Absolute HTTPS root address, without credentials, query or fragment. Development may explicitly allow literal loopback HTTP.";
            }
        }

        return Task.CompletedTask;
    }
}
