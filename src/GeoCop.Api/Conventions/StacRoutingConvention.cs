using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace GeoCop.Api.Conventions;

/// <summary>
/// Customizes the routing conventions for STAC actions to use <c>/api/stac</c> as the base path.
/// </summary>
public class StacRoutingConvention : IActionModelConvention
{
    private const string BaseNamespace = nameof(Stac);
    private const string Template = "/api/stac";
    private readonly AttributeRouteModel routePrefix = new () { Template = Template };

    /// <inheritdoc/>
    public void Apply(ActionModel action)
    {
        var controller = action.Controller;

        var controllerFullName = controller.ControllerType.FullName;
        if (controllerFullName == null || !controllerFullName.StartsWith(BaseNamespace + ".", StringComparison.Ordinal))
            return;

        foreach (var selector in action.Selectors)
        {
            // If the action has a route attribute, prefix it with /api/stac.
            // Actions that are not mapped to a route are ignored.
            if (selector.AttributeRouteModel != null)
            {
                selector.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(routePrefix, selector.AttributeRouteModel);
            }
        }
    }
}
