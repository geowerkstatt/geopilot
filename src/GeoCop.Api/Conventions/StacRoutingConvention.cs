using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace GeoCop.Api.Conventions;

/// <summary>
/// Customizes the routing conventions for STAC actions to use <c>/api/stac</c> as the base path.
/// </summary>
public class StacRoutingConvention : IActionModelConvention
{
    private const string BaseNamespace = nameof(Stac);
    private const string Template = "/api/stac";
    private readonly AttributeRouteModel routePrefix = new () { Template = Template };
    private readonly AuthorizeFilter authorizeFilter;

    /// <summary>
    /// Initializes a new instance of the <see cref="StacRoutingConvention"/> class.
    /// </summary>
    /// <param name="authorizationPolicy">Authorization policy to use for the STAC endpoints.</param>
    public StacRoutingConvention(string authorizationPolicy)
    {
        authorizeFilter = new AuthorizeFilter(authorizationPolicy);
    }

    /// <inheritdoc/>
    public void Apply(ActionModel action)
    {
        var controller = action.Controller;

        var controllerFullName = controller.ControllerType.FullName;
        if (controllerFullName == null || !controllerFullName.StartsWith(BaseNamespace + ".", StringComparison.Ordinal))
            return;

        if (!controller.Filters.Contains(authorizeFilter))
            controller.Filters.Add(authorizeFilter);

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
