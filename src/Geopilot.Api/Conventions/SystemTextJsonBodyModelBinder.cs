using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Geopilot.Api.Conventions;

/// <summary>
/// Parses the model from the request body using System.Text.Json.
/// </summary>
public class SystemTextJsonBodyModelBinder : IModelBinder
{
    /// <inheritdoc/>
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        try
        {
            var request = bindingContext.HttpContext.Request;
            var jsonOptions = bindingContext.HttpContext.RequestServices.GetRequiredService<IOptions<JsonOptions>>();
            var serializerOptions = jsonOptions.Value.JsonSerializerOptions;
            var deserialized = await JsonSerializer.DeserializeAsync(request.Body, bindingContext.ModelType, serializerOptions);
            bindingContext.Result = ModelBindingResult.Success(deserialized!);
        }
        catch (Exception ex)
        {
            bindingContext.ModelState.AddModelError(bindingContext.ModelName, ex, bindingContext.ModelMetadata);
        }
    }
}
