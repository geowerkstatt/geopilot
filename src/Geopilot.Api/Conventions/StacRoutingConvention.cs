using Geopilot.Api.StacServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace Geopilot.Api.Conventions;

/// <summary>
/// Customizes the routing conventions for STAC actions to use <c>/api/stac</c> as the base path.
/// Reads are anonymous, filtered per caller by <see cref="StacAudienceFilter"/>.
/// </summary>
public class StacRoutingConvention : IActionModelConvention
{
    private const string BaseNamespace = nameof(Stac);
    private const string Template = "/api/stac";
    private readonly AttributeRouteModel routePrefix = new() { Template = Template };
    private readonly AuthorizeFilter authorizeFilter;

    /// <summary>
    /// Initializes a new instance of the <see cref="StacRoutingConvention"/> class.
    /// </summary>
    /// <param name="authorizationPolicy">Authorization policy to use for the STAC endpoints.</param>
    public StacRoutingConvention(string authorizationPolicy)
    {
        authorizeFilter = new AuthorizeFilter(authorizationPolicy);
    }

    /// <summary>
    /// Whether a STAC action only reads: GET and HEAD, and the item search, which the STAC API offers as POST.
    /// Reads are anonymous and filtered per caller; everything else stays behind the configured policy.
    /// </summary>
    internal static bool IsRead(string httpMethod, string? template) =>
        HttpMethods.IsGet(httpMethod)
        || HttpMethods.IsHead(httpMethod)
        || (HttpMethods.IsPost(httpMethod) && string.Equals(template?.TrimEnd('/').Split('/')[^1], "search", StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc/>
    public void Apply(ActionModel action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var controller = action.Controller;

        var controllerFullName = controller.ControllerType.FullName;
        if (controllerFullName == null || !controllerFullName.StartsWith(BaseNamespace + ".", StringComparison.Ordinal))
            return;

        if (!controller.Filters.Contains(authorizeFilter))
            controller.Filters.Add(authorizeFilter);

        if (!controller.Filters.OfType<StacAudienceFilter>().Any())
            controller.Filters.Add(new StacAudienceFilter());

        foreach (var selector in action.Selectors)
        {
            var httpMethods = selector.ActionConstraints.OfType<HttpMethodActionConstraint>().SelectMany(c => c.HttpMethods).ToList();
            if (httpMethods.Count > 0 && httpMethods.All(m => IsRead(m, selector.AttributeRouteModel?.Template)))
            {
                // Endpoint metadata, not an MVC filter: the authorization middleware applies the Admin fallback policy otherwise.
                selector.EndpointMetadata.Add(new AllowAnonymousAttribute());
            }

            // If the action has a route attribute, prefix it with /api/stac.
            // Actions that are not mapped to a route are ignored.
            if (selector.AttributeRouteModel != null)
            {
                selector.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(routePrefix, selector.AttributeRouteModel);
            }
        }
    }
}
