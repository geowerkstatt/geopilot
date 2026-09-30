using Geopilot.Api.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace Geopilot.Api;

/// <summary>
/// Describes the request body of an action marked with <see cref="MultipartRequestBodyAttribute"/> as a
/// multipart form of the named type. Such an action reads its parts from the stream and binds no parameter,
/// which would leave the published API without a body for it.
/// </summary>
public class MultipartRequestBodyOperationFilter : IOperationFilter
{
    /// <inheritdoc/>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var attribute = context.MethodInfo.GetCustomAttribute<MultipartRequestBodyAttribute>();
        if (attribute is null)
            return;

        var schema = context.SchemaGenerator.GenerateSchema(attribute.FormType, context.SchemaRepository);
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Description = "The form fields first, then the files. A field that arrives after a file is refused.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType { Schema = schema },
            },
        };
    }
}
