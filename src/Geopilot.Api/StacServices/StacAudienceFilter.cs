using Geopilot.Api.Authorization;
using Geopilot.Api.Models;
using Geopilot.Api.Reactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

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
/// What the STAC catalog shows a caller.
/// </summary>
public static class StacVisibilityExtensions
{
    /// <summary>
    /// The mandates the caller sees, all of them for an administrator and only <see cref="StacAccess.Public"/> ones
    /// otherwise, each with the deliveries the catalog shows (see <see cref="DeliveriesInStac"/>).
    /// </summary>
    public static IQueryable<Mandate> MandatesInStac(this Context context, bool isAdmin, DeliveryOptions deliveryOptions)
    {
        ArgumentNullException.ThrowIfNull(context);

        var shown = ShownDeliveryIds(context, deliveryOptions);
        var mandates = context.MandatesWithIncludes.Include(m => m.Deliveries.Where(d => shown.Contains(d.Id)));
        return isAdmin ? mandates : mandates.Where(m => m.StacAccess == StacAccess.Public);
    }

    /// <summary>
    /// The deliveries of the mandates the caller sees, each once its uploader can no longer delete it and all its
    /// transfers are done. A delivery without transfers appears when its delete window closes, and never while that
    /// window stays open. That holds for administrators as well.
    /// </summary>
    public static IQueryable<Delivery> DeliveriesInStac(this Context context, bool isAdmin, DeliveryOptions deliveryOptions)
    {
        ArgumentNullException.ThrowIfNull(context);

        var shown = ShownDeliveryIds(context, deliveryOptions);
        return context.DeliveriesWithIncludes.Where(d => (isAdmin || d.Mandate.StacAccess == StacAccess.Public) && shown.Contains(d.Id));
    }

    // Nothing is published while it can still be taken back (ADR 0015): the uploader may still delete it, or a transfer is open.
    private static IQueryable<int> ShownDeliveryIds(Context context, DeliveryOptions deliveryOptions)
    {
        ArgumentNullException.ThrowIfNull(deliveryOptions);

        var unforwarded = context.DeliveryReactions.UnforwardedDeliveryIds();
        return context.Deliveries
            .Where(deliveryOptions.DeleteWindowClosedAt(DateTime.UtcNow))
            .Where(d => !unforwarded.Contains(d.Id))
            .Select(d => d.Id);
    }
}
