using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Moongate.Admin.Api.Types.Authentication;

namespace Moongate.Admin.Api.Services.Documentation;

public sealed class AdminOpenApiTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken
    )
    {
        document.Info.Title = "Moongate Admin REST API";
        document.Info.Version = "v1";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["AdminBearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
            Description = "REST JWT returned by login. Send Authorization: Bearer; the upstream token remains private."
        };
        foreach (var path in document.Paths)
        {
            if (path.Value.Operations is not { } operations || !path.Key.StartsWith("/api/", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var method in operations)
            {
                var operation = method.Value;
                operation.Security =
                [
                    new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("AdminBearer", document)] = [] }
                ];
                if (path.Key is "/api/auth/login" or "/api/auth/logout")
                {
                    operation.Security.Add(new OpenApiSecurityRequirement());
                }

                operation.Responses ??= new OpenApiResponses();
                foreach (var status in new[] { "400", "401", "403", "404", "409", "429", "500", "501", "502", "503", "504" })
                {
                    operation.Responses.TryAdd(
                        status,
                        new OpenApiResponse
                        {
                            Description =
                                "Safe Problem Details with code and correlationId. Creation transport failures may include mutationOutcomeUnknown.",
                            Content = new Dictionary<string, OpenApiMediaType>
                            {
                                ["application/problem+json"] = new OpenApiMediaType
                                { Schema = new OpenApiSchema { Type = JsonSchemaType.Object } }
                            }
                        }
                    );
                }
            }
        }

        return Task.CompletedTask;
    }
}
