using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Company.Product.Api.OpenApi;

public sealed class StrictRequestSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.Namespace?.StartsWith(
                "Company.Product.Contracts.Requests",
                StringComparison.Ordinal) == true)
        {
            schema.AdditionalPropertiesAllowed = false;
        }
    }
}
