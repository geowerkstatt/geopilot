namespace GeoCop.Api.StacServices;

/// <summary>
/// Link generator for generating https links.
/// </summary>
public class HttpsLinkGenerator : LinkGenerator
{
    private readonly LinkGenerator defaultLinkGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpsLinkGenerator"/> class.
    /// </summary>
    /// <param name="defaultLinkGenerator">The default <see cref="LinkGenerator"/>.</param>
    public HttpsLinkGenerator(LinkGenerator defaultLinkGenerator)
        => this.defaultLinkGenerator = defaultLinkGenerator;

    /// <inheritdoc />
    public override string? GetPathByAddress<TAddress>(HttpContext httpContext, TAddress address, RouteValueDictionary values, RouteValueDictionary? ambientValues = null, PathString? pathBase = null, FragmentString fragment = default, LinkOptions? options = null)
        => ReplaceHttpWithHttps(defaultLinkGenerator.GetPathByAddress(httpContext, address, values, ambientValues, pathBase, fragment, options));

    /// <inheritdoc />
    public override string? GetPathByAddress<TAddress>(TAddress address, RouteValueDictionary values, PathString pathBase = default, FragmentString fragment = default, LinkOptions? options = null)
        => ReplaceHttpWithHttps(defaultLinkGenerator.GetPathByAddress(address, values, pathBase, fragment, options));

    /// <inheritdoc />
    public override string? GetUriByAddress<TAddress>(HttpContext httpContext, TAddress address, RouteValueDictionary values, RouteValueDictionary? ambientValues = null, string? scheme = null, HostString? host = null, PathString? pathBase = null, FragmentString fragment = default, LinkOptions? options = null)
        => ReplaceHttpWithHttps(defaultLinkGenerator.GetUriByAddress(httpContext, address, values, ambientValues, scheme, host, pathBase, fragment, options));

    /// <inheritdoc />
    public override string? GetUriByAddress<TAddress>(TAddress address, RouteValueDictionary values, string? scheme, HostString host, PathString pathBase = default, FragmentString fragment = default, LinkOptions? options = null)
        => ReplaceHttpWithHttps(defaultLinkGenerator.GetUriByAddress(address, values, scheme, host, pathBase, fragment, options));

    private string? ReplaceHttpWithHttps(string? url)
    {
        if (url?.Contains("http://", StringComparison.Ordinal) ?? false)
        {
            url = url.Replace("http://", "https://");
        }

        return url;
    }
}
