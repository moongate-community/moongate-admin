using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Moongate.Admin.Api.Data.Accounts;
using Moongate.Admin.Api.Data.Configuration;

namespace Moongate.Admin.Api.Services.Documentation;

public sealed class AdminCredentialSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken
    )
    {
        var probe = context.JsonTypeInfo.Type == typeof(ConnectionProbeRequest);
        if (!probe && context.JsonTypeInfo.Type != typeof(LoginRequest) &&
            context.JsonTypeInfo.Type != typeof(CreateAccountRequest))
        {
            return Task.CompletedTask;
        }

        schema.Required ??= new HashSet<string>();
        foreach (var name in new[] { "username", "password" })
        {
            schema.Required.Add(name);
            if (schema.Properties?.TryGetValue(name, out var value) == true && value is OpenApiSchema property)
            {
                property.Type = JsonSchemaType.String;
                property.MinLength = 1;
                if (probe)
                {
                    property.WriteOnly = true;
                }

                if (name == "password")
                {
                    property.WriteOnly = true;
                    property.Description =
                        "Nonblank, no NUL characters, at most 1024 UTF-8 bytes. Supplied at runtime from the secret store.";
                }
                else
                {
                    property.Description =
                        "Case and whitespace are preserved. Nonblank, no NUL characters, at most 255 UTF-16 code units.";
                }
            }
        }

        return Task.CompletedTask;
    }
}
