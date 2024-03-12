using Stac;
using Stac.Api.Interfaces;

namespace Geopilot.Api.StacServices;

/// <summary>
/// STAC API context with the custom <see cref="HttpsLinkGenerator"/>.
/// </summary>
public class HttpsStacApiContext : IStacApiContext, IStacPropertiesContainer
{
    /// <inheritdoc />
    public Uri? BaseUri { get; private set; }

    /// <inheritdoc />
    public LinkGenerator? LinkGenerator { get; private set; }

    /// <inheritdoc />
    public HttpContext? HttpContext { get; private set; }

    /// <inheritdoc />
    public IList<string>? Collections { get; private set; }

    /// <summary>
    /// Gets the number of matched items.
    /// </summary>
    public int MatchedItemsCount { get; private set; }

    /// <inheritdoc />
    public IList<ILinkValues> LinkValues { get; } = new List<ILinkValues>();

    /// <inheritdoc />
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();

    /// <inheritdoc />
    public IStacObject? StacObjectContainer => null;

    /// <inheritdoc />
    public string? Id => HttpContext?.TraceIdentifier;

    /// <summary>
    /// Creates a new instance of the <see cref="HttpsStacApiContext"/> class.
    /// </summary>
    public static HttpsStacApiContext Create(HttpContext httpContext)
    {
        return new HttpsStacApiContext
        {
            BaseUri = new Uri("https://" + httpContext.Request.Host),
            LinkGenerator = new HttpsLinkGenerator((httpContext.RequestServices.GetService(typeof(LinkGenerator)) as LinkGenerator) !),
            HttpContext = httpContext,
        };
    }

    /// <inheritdoc />
    public void SetCollections(IList<string> collectionId) => Collections = collectionId;

    /// <summary>
    /// Sets the number of matched items.
    /// </summary>
    public void SetMatchedItemsCount(int count) => MatchedItemsCount = count;
}
