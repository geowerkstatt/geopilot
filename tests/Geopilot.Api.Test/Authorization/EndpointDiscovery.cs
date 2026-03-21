using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Globalization;
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
                    // Mirrors DefaultPolicy = adminPolicy and FallbackPolicy = adminPolicy in Program.cs.
                    policy = GeopilotPolicies.Admin;
                }

                if (anonymous != isAnonymous)
                    continue;

                var controllerName = controller.Name.Replace("Controller", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
                var versions = controller.GetCustomAttribute<ApiVersionAttribute>()?.Versions;
                var apiVersion = versions is { Count: > 0 } ? versions[0].MajorVersion?.ToString(CultureInfo.InvariantCulture) ?? "1" : "1";
                var description = $"{controller.Name}.{method.Name}";

                foreach (var httpMethodAttr in httpMethodAttrs)
                {
                    var httpMethod = httpMethodAttr.HttpMethods.First();
                    var route = BuildRoute(routePrefix, httpMethodAttr.Template, controllerName, apiVersion);

                    yield return (httpMethod, route, policy, description);
                }
            }
        }

        foreach (var endpoint in DiscoverStacEndpoints(anonymous))
        {
            yield return endpoint;
        }
    }

    private static IEnumerable<(string HttpMethod, string Url, string? Policy, string Description)> DiscoverStacEndpoints(bool anonymous)
    {
        const string stacRoutePrefix = "/api/stac";

        var stacControllers = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null).Select(t => t!); }
            })
            .Where(t => !t.IsAbstract && !t.IsInterface
                && typeof(ControllerBase).IsAssignableFrom(t)
                && t.FullName?.StartsWith("Stac.", StringComparison.Ordinal) == true);

        foreach (var controller in stacControllers)
        {
            var classRouteAttr = controller.GetCustomAttribute<RouteAttribute>();
            var classRoute = classRouteAttr?.Template ?? string.Empty;
            var classAllowAnonymous = controller.GetCustomAttribute<AllowAnonymousAttribute>() != null;
            var controllerName = controller.Name.Replace("Controller", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

            var methods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                var httpMethodAttrs = method.GetCustomAttributes()
                    .OfType<HttpMethodAttribute>()
                    .ToList();

                if (httpMethodAttrs.Count == 0)
                    continue;

                var methodAllowAnonymous = method.GetCustomAttribute<AllowAnonymousAttribute>() != null;
                var isAnonymous = classAllowAnonymous || methodAllowAnonymous;

                if (anonymous != isAnonymous)
                    continue;

                var policy = isAnonymous ? null : GeopilotPolicies.Admin;
                var description = $"{controller.Name}.{method.Name}";

                foreach (var httpMethodAttr in httpMethodAttrs)
                {
                    var httpMethod = httpMethodAttr.HttpMethods.First();
                    var methodTemplate = httpMethodAttr.Template;
                    var combinedTemplate = CombineTemplates(classRoute, methodTemplate);
                    var route = BuildRoute(stacRoutePrefix, combinedTemplate, controllerName);

                    yield return (httpMethod, route, policy, description);
                }
            }
        }
    }

    private static string? CombineTemplates(string classRoute, string? methodTemplate)
    {
        if (string.IsNullOrEmpty(classRoute))
            return methodTemplate;
        if (string.IsNullOrEmpty(methodTemplate))
            return classRoute;
        return $"{classRoute}/{methodTemplate}";
    }

    private static string BuildRoute(string prefix, string? template, string controllerName, string apiVersion = "1")
    {
        var route = string.IsNullOrEmpty(template) ? prefix : $"{prefix}/{template}";

        route = route.Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase);
        route = route.Replace("v{version:apiVersion}", $"v{apiVersion}", StringComparison.OrdinalIgnoreCase);
        route = RouteParameterRegex.Replace(route, "1");

        if (!route.StartsWith('/'))
            route = "/" + route;

        return route;
    }
}
