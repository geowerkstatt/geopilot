using Stac;
using Stac.Api.Interfaces;

namespace Geopilot.Api.StacServices;

/// <summary>
/// Handles STAC items changes.
/// </summary>
public class StacItemsBroker : IItemsBroker
{
    /// <inheritdoc/>
    public Task<StacItem> CreateItemAsync(StacItem stacItem, IStacApiContext stacApiContext, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task DeleteItemAsync(string featureId, IStacApiContext stacApiContext, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<IEnumerable<StacCollection>> RefreshStacCollectionsAsync(IStacApiContext stacApiContext, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    Task<StacItem> IItemsBroker.UpdateItemAsync(StacItem newItem, string featureId, IStacApiContext stacApiContext, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
