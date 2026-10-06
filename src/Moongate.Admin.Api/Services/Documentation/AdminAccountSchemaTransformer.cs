using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Moongate.Admin.Api.Types.Accounts;

namespace Moongate.Admin.Api.Services.Documentation;

public sealed class AdminAccountSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type == typeof(AdminAccountType))
        {
            schema.Type = JsonSchemaType.String;
            schema.Enum = [JsonValue.Create("regular"), JsonValue.Create("gameMaster"), JsonValue.Create("administrator")];
        }
        return Task.CompletedTask;
    }
}
