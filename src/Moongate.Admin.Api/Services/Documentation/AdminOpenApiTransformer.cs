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
        document.Components.SecuritySchemes["SetupToken"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "X-Moongate-Setup-Token",
            Description =
                "Temporary operator token supplied from Bitwarden at runtime. Only usable while unconfigured; never stored in the catalog."
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

                if (path.Key == "/api/configuration/status")
                {
                    operation.Security.Clear();
                }

                if (path.Key == "/api/configuration/setup")
                {
                    operation.Security =
                    [
                        new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("SetupToken", document)] = [] }
                    ];
                }

                if (path.Key == "/api/configuration/test-connection")
                {
                    operation.Security.Add(
                        new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("SetupToken", document)] = [] }
                    );
                }

                if (path.Key == "/api/configuration" &&
                    method.Key.ToString().Equals("PUT", StringComparison.OrdinalIgnoreCase))
                {
                    operation.Parameters ??= [];
                    operation.Parameters.Add(
                        new OpenApiParameter
                        {
                            Name = "If-Match", In = ParameterLocation.Header, Required = true,
                            Description =
                                "Quoted strong ETag from the last configuration read. A successful update requires another login.",
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                        }
                    );
                }

                operation.Responses ??= new OpenApiResponses();
                foreach (var status in new[]
                         {
                             "400", "401", "403", "404", "409", "412", "413", "428", "429", "500", "501", "502", "503", "504"
                         })
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

                if (path.Key is "/api/configuration" or "/api/configuration/setup")
                {
                    var success = path.Key == "/api/configuration/setup" ? "201" : "200";
                    if (operation.Responses.TryGetValue(success, out var value) && value is OpenApiResponse response)
                    {
                        response.Headers ??= new Dictionary<string, IOpenApiHeader>();
                        response.Headers["ETag"] = new OpenApiHeader
                        {
                            Description = "Opaque configuration revision, quoted for subsequent If-Match.",
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                        };
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}
