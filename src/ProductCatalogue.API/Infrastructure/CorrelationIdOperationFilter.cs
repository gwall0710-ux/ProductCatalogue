using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ProductCatalogue.API.Infrastructure;

/// <summary>
/// Adds X-Correlation-ID as an optional header parameter to every
/// endpoint in the Swagger UI, so testers can supply their own ID
/// and trace it end-to-end through the logs.
/// </summary>
public sealed class CorrelationIdOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name        = "X-Correlation-ID",
            In          = ParameterLocation.Header,
            Required    = false,
            Schema      = new OpenApiSchema { Type = "string", Format = "uuid" },
            Description = "Optional correlation ID for end-to-end request tracing. " +
                          "Auto-generated if not supplied."
        });
    }
}
