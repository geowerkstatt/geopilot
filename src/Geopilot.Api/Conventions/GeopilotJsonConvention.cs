using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Conventions;

/// <summary>
/// Customizes the convention for Geopilot actions to use System.Text.Json.
/// </summary>
public class GeopilotJsonConvention : IActionFilter, IActionModelConvention
{
    private const string BaseNamespace = nameof(Geopilot);
    private FormatterCollection<IOutputFormatter>? outputFormatters;

    /// <inheritdoc/>
    public void Apply(ActionModel action)
    {
        var controller = action.Controller;

        var controllerFullName = controller.ControllerType.FullName;
        if (controllerFullName == null || !controllerFullName.StartsWith(BaseNamespace + ".", StringComparison.Ordinal))
            return;

        // Based on https://www.planetgeek.ch/2022/10/15/using-system-text-json-alongside-newtonsoft-json-net/
        // Set custom model binder to every parameter that uses [FromBody]
        var parameters = action.Parameters.Where(p => p.BindingInfo?.BindingSource == BindingSource.Body);
        foreach (var p in parameters)
        {
            p.BindingInfo!.BinderType = typeof(SystemTextJsonBodyModelBinder);
        }

        action.Filters.Add(this);
    }

    /// <inheritdoc/>
    public void OnActionExecuting(ActionExecutingContext context)
    {
    }

    /// <inheritdoc/>
    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is ObjectResult objectResult)
        {
            if (outputFormatters == null)
            {
                var jsonOptions = context.HttpContext.RequestServices.GetRequiredService<IOptions<JsonOptions>>();
                var serializerOptions = jsonOptions.Value.JsonSerializerOptions;
                var formatter = new SystemTextJsonOutputFormatter(serializerOptions);

                var mvcOptions = context.HttpContext.RequestServices.GetRequiredService<IOptions<MvcOptions>>();
                var mvcOutputFormattersCopy = new List<IOutputFormatter>(mvcOptions.Value.OutputFormatters);

                outputFormatters = new FormatterCollection<IOutputFormatter>(mvcOutputFormattersCopy);
                outputFormatters.RemoveType<NewtonsoftJsonOutputFormatter>();
                outputFormatters.Add(formatter);
            }

            objectResult.Formatters = outputFormatters;
        }
    }
}
