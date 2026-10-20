using Geopilot.Api.Authorization;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Geopilot.Api.StacServices;

/// <summary>
/// Records per request whether the caller is an administrator, who sees every mandate in the STAC catalog.
/// Everybody else sees only mandates with <see cref="StacAccess.Public"/>.
/// </summary>
public sealed class StacAudienceFilter : IAsyncActionFilter
{
    private const string IsAdminKey = "Geopilot.StacIsAdmin";

    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        // Resolved per request: the Admin handler depends on scoped services a singleton must not capture.
        var httpContext = context.HttpContext;
        var authorizationService = httpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        httpContext.Items[IsAdminKey] = (await authorizationService.AuthorizeAsync(httpContext.User, GeopilotPolicies.Admin)).Succeeded;

        await next();
    }

    /// <summary>
    /// Whether the request was recorded as coming from an administrator. False when nothing was recorded.
    /// </summary>
    public static bool IsAdmin(HttpContext? httpContext) => httpContext?.Items[IsAdminKey] is true;
}

/// <summary>
/// Restricts mandates to those the caller may see in the STAC catalog.
/// </summary>
public static class StacVisibilityExtensions
{
    /// <summary>
    /// All mandates for an administrator, only <see cref="StacAccess.Public"/> ones otherwise.
    /// </summary>
    public static IQueryable<Mandate> VisibleInStac(this IQueryable<Mandate> mandates, bool isAdmin)
        => isAdmin ? mandates : mandates.Where(m => m.StacAccess == StacAccess.Public);
}
