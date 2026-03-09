using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Geopilot.Api.Authorization;

internal static class EndpointDiscovery
{
    private static readonly Regex RouteParameterRegex = new(@"\{[^}]+\}", RegexOptions.Compiled);

    public static IEnumerable<object[]> GetProtectedEndpoints()
    {
        foreach (var (httpMethod, url, policy, description) in DiscoverEndpoints(anonymous: false))
        {
            yield return new object[] { httpMethod, url, policy!, description };
        }
    }

    public static IEnumerable<object[]> GetAdminEndpoints()
    {
        return GetProtectedEndpoints().Where(e => (string)e[2] == GeopilotPolicies.Admin);
    }

    public static IEnumerable<object[]> GetUserEndpoints()
    {
        return GetProtectedEndpoints().Where(e => (string)e[2] == GeopilotPolicies.User);
    }

    public static IEnumerable<object[]> GetAnonymousEndpoints()
    {
        foreach (var (httpMethod, url, _, description) in DiscoverEndpoints(anonymous: true))
        {
            yield return new object[] { httpMethod, url, description };
        }
    }

    private static IEnumerable<(string HttpMethod, string Url, string? Policy, string Description)> DiscoverEndpoints(bool anonymous)
    {
        var controllers = typeof(Context).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t) && t.Namespace?.StartsWith("Geopilot.Api", StringComparison.Ordinal) == true);

        foreach (var controller in controllers)
        {
            var routeAttr = controller.GetCustomAttribute<RouteAttribute>();
            if (routeAttr == null)
                continue;

            var routePrefix = routeAttr.Template ?? string.Empty;
            var classAllowAnonymous = controller.GetCustomAttribute<AllowAnonymousAttribute>() != null;

            var methods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                var httpMethodAttrs = method.GetCustomAttributes()
                    .OfType<HttpMethodAttribute>()
                    .ToList();

                if (httpMethodAttrs.Count == 0)
                    continue;

                var methodAllowAnonymous = method.GetCustomAttribute<AllowAnonymousAttribute>() != null;
                var authorizeAttr = method.GetCustomAttribute<AuthorizeAttribute>();

                var isAnonymous = classAllowAnonymous || methodAllowAnonymous;

                string? policy;
                if (isAnonymous)
                {
                    policy = null;
                }
                else if (authorizeAttr?.Policy != null)
                {
                    policy = authorizeAttr.Policy;
                }
                else
                {
                    policy = GeopilotPolicies.Admin;
                }

                if (anonymous != isAnonymous)
                    continue;

                var controllerName = controller.Name.Replace("Controller", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
                var description = $"{controller.Name}.{method.Name}";

                foreach (var httpMethodAttr in httpMethodAttrs)
                {
                    var httpMethod = httpMethodAttr.HttpMethods.First();
                    var route = BuildRoute(routePrefix, httpMethodAttr.Template, controllerName);

                    yield return (httpMethod, route, policy, description);
                }
            }
        }
    }

    private static string BuildRoute(string prefix, string? template, string controllerName)
    {
        var route = string.IsNullOrEmpty(template) ? prefix : $"{prefix}/{template}";

        route = route.Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase);
        route = route.Replace("v{version:apiVersion}", "v1", StringComparison.OrdinalIgnoreCase);
        route = RouteParameterRegex.Replace(route, "1");

        if (!route.StartsWith('/'))
            route = "/" + route;

        return route;
    }
}
