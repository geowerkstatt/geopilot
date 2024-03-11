using Stac;
using Stac.Api.Interfaces;

namespace Geopilot.Api.StacServices;

/// <summary>
/// Provides access to STAC root catalog.
/// </summary>
public class StacRootCatalogProvider : IRootCatalogProvider
{
    /// <inheritdoc/>
    public Task<StacCatalog> GetRootCatalogAsync(IStacApiContext stacApiContext, CancellationToken cancellationToken = default)
    {
        var catalog = new StacCatalog("geopilot", "Root catalog for geopilot");
        return Task.FromResult(catalog);
    }
}
