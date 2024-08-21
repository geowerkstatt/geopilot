using Stac;
using Stac.Api.Interfaces;
using Stac.Api.WebApi.Implementations.Default;

namespace Geopilot.Api.StacServices;

/// <summary>
/// Provides access to STAC API context with the custom <see cref="HttpsLinkGenerator"/>.
/// </summary>
public class HttpsStacApiContextFactory : IStacApiContextFactory
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IStacApiContextFiltersProvider stacApiContextFilterProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpsStacApiContextFactory"/> class.
    /// </summary>
    public HttpsStacApiContextFactory(IHttpContextAccessor httpContextAccessor, IStacApiContextFiltersProvider stacApiContextFilterProvider)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.stacApiContextFilterProvider = stacApiContextFilterProvider;
    }

    /// <inheritdoc />
    public IEnumerable<T> ApplyContextPostQueryFilters<T>(IStacApiContext stacApiContext, IDataProvider<T> dataProvider, IEnumerable<T> items)
        where T : IStacObject
    {
        // Show the number of items in the search results
        stacApiContext.Properties[DefaultConventions.MatchedCountPropertiesKey] = items.Count();

        IEnumerable<T> filteredItems = items;
        foreach (IStacApiContextFilter stacApiContextFilter in stacApiContextFilterProvider.GetFilters<T>())
        {
            filteredItems = stacApiContextFilter.ApplyContextPostQueryFilters(stacApiContext, dataProvider, filteredItems);
        }

        return filteredItems;
    }

    /// <inheritdoc />
    public T ApplyContextPostQueryFilters<T>(IStacApiContext stacApiContext, IDataProvider<T> dataProvider, T item)
        where T : IStacObject
    {
        T filteredItem = item;
        foreach (IStacApiContextFilter stacApiContextFilter in stacApiContextFilterProvider.GetFilters<T>())
        {
            filteredItem = stacApiContextFilter.ApplyContextPostQueryFilters(stacApiContext, dataProvider, filteredItem);
        }

        return filteredItem;
    }

    /// <inheritdoc />
    public void ApplyContextPreQueryFilters<T>(IStacApiContext stacApiContext, IDataProvider<T> dataProvider, IStacApiRequestBody body)
        where T : IStacObject
    {
        foreach (IStacApiContextFilter stacApiContextFilter in stacApiContextFilterProvider.GetFilters<T>())
        {
            stacApiContextFilter.ApplyContextPreQueryFilters(stacApiContext, dataProvider, body);
        }
    }

    /// <inheritdoc />
    public void ApplyContextResultFilters<T>(IStacApiContext stacApiContext, IDataProvider<T> dataProvider, IStacResultObject<T> result)
        where T : IStacObject
    {
        foreach (IStacApiContextFilter stacApiContextFilter in stacApiContextFilterProvider.GetFilters<T>())
        {
            stacApiContextFilter.ApplyContextResultFilters(stacApiContext, dataProvider, result);
        }
    }

    /// <summary>
    /// Creates a new <see cref="IStacApiContext"/> instance.
    /// </summary>
    public IStacApiContext Create() => HttpsStacApiContext.Create(httpContextAccessor.HttpContext!);
}
