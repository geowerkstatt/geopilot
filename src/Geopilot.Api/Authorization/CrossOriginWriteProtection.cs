using System.Web;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Protects the geopilot.auth cookie against cross-site request forgery. SameSite only keeps the cookie from foreign
/// sites, a page on a sibling subdomain of the same site still makes the browser send it along.
/// </summary>
public static class CrossOriginWriteProtection
{
    /// <summary>
    /// Refuses with 403 any write that carries the geopilot.auth cookie but comes from another origin. Requests without
    /// the cookie, such as bearer clients, pass untouched.
    /// </summary>
    public static void UseCrossOriginWriteProtection(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CrossOriginWriteProtection));
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            var isWrite = !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method);
            if (isWrite && !string.IsNullOrEmpty(request.Cookies[AuthDefaults.AuthCookieName]) && !IsSameOriginRequest(request))
            {
                logger.LogWarning(
                    "Refused {Method} <{Path}> carrying the auth cookie from another origin (Sec-Fetch-Site <{FetchSite}>, Origin <{Origin}>, Host <{Host}>).",
                    HttpUtility.HtmlEncode(request.Method),
                    HttpUtility.HtmlEncode(request.Path.Value),
                    HttpUtility.HtmlEncode(request.Headers["Sec-Fetch-Site"].ToString()),
                    HttpUtility.HtmlEncode(request.Headers.Origin.ToString()),
                    HttpUtility.HtmlEncode(request.Host.Value));
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next(context);
        });
    }

    private static bool IsSameOriginRequest(HttpRequest request)
    {
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        if (fetchSite.Length > 0)
            return string.Equals(fetchSite, "same-origin", StringComparison.Ordinal);

        // Browsers from before 2023 send no fetch metadata, but every browser sends Origin on a cross-origin write.
        // Without either header the caller is no browser, so no foreign page can have triggered the request.
        var origin = request.Headers.Origin.ToString();
        return origin.Length == 0
            || (Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
                && string.Equals(originUri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase));
    }
}
