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
        document.Components.SecuritySchemes["AdminSession"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie, Name = AdminAuthentication.Cookie,
            Description = "Opaque session reference. Obtain it through login; upstream tokens are never exposed."
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
                if (path.Key is not ("/api/auth/csrf" or "/api/auth/login" or "/api/auth/logout"))
                {
                    operation.Security =
                    [
                        new OpenApiSecurityRequirement
                            { [new OpenApiSecuritySchemeReference("AdminSession", document)] = [] }
                    ];
                }

                if (method.Key.ToString().Equals("POST", StringComparison.OrdinalIgnoreCase))
                {
                    operation.Parameters ??= [];
                    operation.Parameters.Add(
                        new OpenApiParameter
                        {
                            Name = AdminAuthentication.CsrfHeader, In = ParameterLocation.Header, Required = true,
                            Description = "Fetch /api/auth/csrf before mutation and after authentication changes.",
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                        }
                    );
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
